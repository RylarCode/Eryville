using System;
using System.IO;
using UnityEngine;

// The stand-in for a server that lives inside the game: saves are files in the device's data folder, the time
// is the device's clock, and the recruitment rolls are done by the GachaSystem in this same process.
// It answers immediately.
public class LocalBackend : IGameBackend
{
    public string Name => "Local";
    public bool ServerOwnsBalances => false;

    public void Connect(Action<BackendResult> done)
    {
        done?.Invoke(BackendResult.Ok());
    }

    public void GetPlayerState(Action<PlayerStateResponse> done)
    {
        done?.Invoke(new PlayerStateResponse()); // nothing to report: the game keeps its own
    }

    public void DevAction(DevRequest request, Action<DevResponse> done)
    {
        done?.Invoke(new DevResponse { ok = false, error = "The local backend has no testing actions: the game changes its own data." });
    }

    public DateTime ServerUtcNow => DateTime.UtcNow;

    public void SyncTime(Action<BackendResult> done)
    {
        done?.Invoke(BackendResult.Ok());
    }

    // --- Saves ---

    private static string PathFor(string slot)
    {
        if (string.IsNullOrEmpty(slot)) slot = SaveSystem.DefaultFileName;
        return Path.Combine(Application.persistentDataPath, Path.GetFileName(slot)); // file name only, no folders
    }

    public void SaveBlob(string slot, string json, Action<BackendResult> done)
    {
        string path = PathFor(slot);
        try
        {
            // Write to a temp file first so a failed write can't destroy the previous save
            string temp = path + ".tmp";
            File.WriteAllText(temp, json);
            File.Copy(temp, path, true);
            File.Delete(temp);

            Debug.Log($"Game saved to {path}");
            done?.Invoke(BackendResult.Ok());
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save game to {path}: {e}");
            done?.Invoke(BackendResult.Fail(e.Message));
        }
    }

    public void LoadBlob(string slot, Action<BackendResult, string> done)
    {
        string path = PathFor(slot);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"No save file at {path}.");
            done?.Invoke(BackendResult.Ok(), null);
            return;
        }

        try
        {
            done?.Invoke(BackendResult.Ok(), File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not read save file {path}: {e.Message}");
            done?.Invoke(BackendResult.Fail(e.Message), null);
        }
    }

    public void HasBlob(string slot, Action<bool> done)
    {
        done?.Invoke(File.Exists(PathFor(slot)));
    }

    public void DeleteBlob(string slot, Action<BackendResult> done)
    {
        string path = PathFor(slot);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"Deleted save file {path}");
            }
            done?.Invoke(BackendResult.Ok());
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not delete {path}: {e.Message}");
            done?.Invoke(BackendResult.Fail(e.Message));
        }
    }

    // --- Recruitment (decided here, in the game, because there is no server) ---

    public void Pull(PullRequest request, Action<PullResponse> done)
    {
        GachaSystem gacha = GachaSystem.Instance;
        done?.Invoke(gacha != null ? gacha.LocalPull(request) : PullResponse.Failed("Recruitment is not available."));
    }

    // There is no server to pay letters here: the game adds them itself (this is not called with the local backend)
    public void ClaimQuest(ClaimQuestRequest request, Action<ClaimQuestResponse> done)
    {
        done?.Invoke(ClaimQuestResponse.Failed("There is no server: the game gives the letters itself."));
    }

    public void BuyLetter(BuyLetterRequest request, Action<BuyLetterResponse> done)
    {
        GachaSystem gacha = GachaSystem.Instance;
        done?.Invoke(gacha != null ? gacha.LocalBuyLetter() : BuyLetterResponse.Failed("Recruitment is not available."));
    }
}
