using System;
using System.Collections.Generic;
using UnityEngine;

// The online backend: the game's server on Firebase. It never touches the database itself. It signs the player in
// (anonymously, see FirebaseAuth) and calls the server's functions (functions/src in the repository) over HTTP:
//   getTime, getPlayerState, loadGame, saveGame, deleteSave, pull, buyLetter, devAction
// The server decides letters, pity and pulls; the game shows what it is told.
//
// Playing without a connection: saves are also kept in a copy on the device (the local backend), so the game
// can still load when the server cannot be reached. Pulls and buying letters need the server.
public class FirebaseBackend : IGameBackend
{
    private readonly FirebaseConfig config;
    private readonly IHttpClient http;
    private readonly FirebaseAuth auth;
    private readonly LocalBackend cache = new LocalBackend();
    private TimeSpan serverOffset = TimeSpan.Zero;

    public FirebaseBackend(FirebaseConfig config, IHttpClient http, Func<DateTime> now = null)
    {
        this.config = config;
        this.http = http;
        auth = new FirebaseAuth(config, http, now);
    }

    public string Name => config.useEmulator ? "Firebase (emulator)" : "Firebase";
    public bool ServerOwnsBalances => true;
    public string UserId => auth.UserId;
    public FirebaseAuth Auth => auth;

    // The device's clock corrected by how far it was found to be from the server's
    public DateTime ServerUtcNow => DateTime.UtcNow + serverOffset;

    // --- Connecting and time ---

    public void Connect(Action<BackendResult> done)
    {
        auth.EnsureSignedIn(result =>
        {
            if (!result.ok) { done?.Invoke(result); return; }
            SyncTime(done);
        });
    }

    public void SyncTime(Action<BackendResult> done)
    {
        DateTime sent = DateTime.UtcNow;
        Call<TimeResponse>("getTime", "{}", response =>
        {
            if (response.ok)
            {
                DateTime received = DateTime.UtcNow;
                DateTime serverNow = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(response.serverMs);
                serverNow += TimeSpan.FromTicks((received - sent).Ticks / 2); // the answer was made halfway through the round trip
                serverOffset = serverNow - received;
            }
            done?.Invoke(response);
        });
    }

    // --- Saves ---

    public void SaveBlob(string slot, string json, Action<BackendResult> done)
    {
        cache.SaveBlob(slot, json, _ => { }); // the copy on this device
        Call<SaveGameResponse>("saveGame", JsonUtility.ToJson(new SaveGameRequest { slot = slot, json = json }), response =>
        {
            if (!response.ok) Debug.LogWarning("Could not save to the server: " + response.error + (response.offline ? " (the copy on this device is kept)" : ""));
            done?.Invoke(response);
        });
    }

    public void LoadBlob(string slot, Action<BackendResult, string> done)
    {
        Call<LoadGameResponse>("loadGame", JsonUtility.ToJson(new LoadGameRequest { slot = slot, includeJson = true }), response =>
        {
            if (response.offline)
            {
                Debug.LogWarning("The server cannot be reached: loading the copy on this device.");
                cache.LoadBlob(slot, done);
                return;
            }
            if (!response.ok) { done?.Invoke(response, null); return; }

            if (response.exists)
            {
                string json = WithServerState(response.json, response.letters, response.totalPulls, response.pity);
                cache.SaveBlob(slot, json, _ => { });
                done?.Invoke(BackendResult.Ok(), json);
                return;
            }

            // Nothing on the server yet. If this device has a save (from before there was a server) it becomes the first one.
            cache.LoadBlob(slot, (cacheResult, localJson) =>
            {
                if (localJson == null) { done?.Invoke(BackendResult.Ok(), null); return; }
                string json = WithServerState(localJson, response.letters, response.totalPulls, response.pity);
                Debug.Log("Uploading this device's save as the first one on the server.");
                SaveBlob(slot, json, _ => { });
                done?.Invoke(BackendResult.Ok(), json);
            });
        });
    }

    public void HasBlob(string slot, Action<bool> done)
    {
        Call<LoadGameResponse>("loadGame", JsonUtility.ToJson(new LoadGameRequest { slot = slot, includeJson = false }), response =>
        {
            if (response.offline) cache.HasBlob(slot, done);
            else done?.Invoke(response.ok && response.exists);
        });
    }

    public void DeleteBlob(string slot, Action<BackendResult> done)
    {
        cache.DeleteBlob(slot, _ => { });
        Call<BackendResult>("deleteSave", JsonUtility.ToJson(new SlotRequest { slot = slot }), response => done?.Invoke(response));
    }

    // Puts the server's letters, pity and pull count into a saved game's text (the server's word is final)
    private static string WithServerState(string json, int letters, int totalPulls, List<int> pity)
    {
        try
        {
            GameData data = JsonUtility.FromJson<GameData>(json);
            if (data == null) return json;

            bool found = false;
            foreach (ResourceData resource in data.resources)
            {
                if (resource.name != ResourceManager.Letters) continue;
                resource.amount = letters;
                found = true;
            }
            if (!found) data.resources.Add(new ResourceData { name = ResourceManager.Letters, amount = letters });
            data.gachaTotalPulls = totalPulls;
            data.gachaPity = pity != null ? new List<int>(pity) : new List<int>();
            return JsonUtility.ToJson(data, true);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Could not put the server's numbers into the save: " + e.Message);
            return json;
        }
    }

    // --- Things the server decides ---

    public void GetPlayerState(Action<PlayerStateResponse> done)
    {
        Call<PlayerStateResponse>("getPlayerState", "{}", done);
    }

    public void Pull(PullRequest request, Action<PullResponse> done)
    {
        Call<PullResponse>("pull", JsonUtility.ToJson(request), done);
    }

    public void BuyLetter(BuyLetterRequest request, Action<BuyLetterResponse> done)
    {
        Call<BuyLetterResponse>("buyLetter", JsonUtility.ToJson(request), done);
    }

    public void ClaimQuest(ClaimQuestRequest request, Action<ClaimQuestResponse> done)
    {
        Call<ClaimQuestResponse>("claimQuest", JsonUtility.ToJson(request), done);
    }

    public void DevAction(DevRequest request, Action<DevResponse> done)
    {
        Call<DevResponse>("devAction", JsonUtility.ToJson(request), done);
    }

    // --- Calling the server ---

    // Calls a function: signs in if needed, sends {"data": ...}, and turns the answer ({"result": ...}) into T.
    // A refused token is renewed and the call tried once more. Problems come back as a failed T, never as an exception.
    private void Call<T>(string function, string dataJson, Action<T> done, bool retried = false) where T : BackendResult, new()
    {
        auth.EnsureSignedIn(signedIn =>
        {
            if (!signedIn.ok) { done?.Invoke(Failure<T>(signedIn.error, signedIn.offline)); return; }

            http.Post(config.FunctionUrl(function), "{\"data\":" + dataJson + "}", "application/json", auth.IdToken, reply =>
            {
                if (reply.networkError || reply.status == 0) { done?.Invoke(Failure<T>("Could not reach the server.", true)); return; }
                if (reply.status == 401 && !retried)
                {
                    auth.Invalidate();
                    Call(function, dataJson, done, true);
                    return;
                }
                done?.Invoke(ParseAnswer<T>(reply));
            });
        });
    }

    private static T ParseAnswer<T>(HttpResult reply) where T : BackendResult, new()
    {
        string result = JsonExtract.Value(reply.body, "result") ?? JsonExtract.Value(reply.body, "data");
        if (reply.status == 200 && result != null)
        {
            try
            {
                T parsed = JsonUtility.FromJson<T>(result);
                if (parsed != null) return parsed;
            }
            catch (Exception e)
            {
                return Failure<T>("The server's answer could not be read: " + e.Message, false);
            }
        }

        string error = JsonExtract.Value(reply.body, "error");
        string message = error != null ? (JsonExtract.Value(error, "message") ?? error).Trim('"') : "HTTP " + reply.status;
        return Failure<T>("The server refused: " + message, false);
    }

    private static T Failure<T>(string message, bool offline) where T : BackendResult, new()
    {
        return new T { ok = false, error = message, offline = offline };
    }
}
