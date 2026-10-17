using System;
using System.Collections.Generic;
using UnityEngine;

// Signs the player in to Firebase without asking for anything: an anonymous account is created the first time and
// remembered on the device (so the same player comes back every run). Talks to the Firebase sign-in service over
// plain HTTP. Later, accounts can be linked to Google Play Games / Apple so progress survives a reinstall or a new phone.
//
// Tokens last an hour; this renews them by itself, and several callers asking at once share one sign-in.
public class FirebaseAuth
{
    public const string RefreshPref = "fb_refresh_token";
    public const string UserPref = "fb_user_id";

    [Serializable] private class SignUpResponse { public string idToken; public string refreshToken; public string expiresIn; public string localId; }
    [Serializable] private class RefreshResponse { public string id_token; public string refresh_token; public string expires_in; public string user_id; }

    private readonly FirebaseConfig config;
    private readonly IHttpClient http;
    private readonly Func<DateTime> now;

    private string idToken = "";
    private string refreshToken;
    private DateTime expiresAt = DateTime.MinValue;
    private bool busy;
    private readonly List<Action<BackendResult>> waiting = new List<Action<BackendResult>>();

    public string UserId { get; private set; }
    public string IdToken => idToken;
    public bool SignedIn => !string.IsNullOrEmpty(idToken) && now() < expiresAt;

    public FirebaseAuth(FirebaseConfig config, IHttpClient http, Func<DateTime> now = null)
    {
        this.config = config;
        this.http = http;
        this.now = now ?? (() => DateTime.UtcNow);
        refreshToken = PlayerPrefs.GetString(RefreshPref, "");
        UserId = PlayerPrefs.GetString(UserPref, "");
    }

    // Makes sure there is a valid token, signing in or renewing it as needed
    public void EnsureSignedIn(Action<BackendResult> done)
    {
        if (SignedIn && now() < expiresAt - TimeSpan.FromSeconds(60))
        {
            done(BackendResult.Ok());
            return;
        }

        waiting.Add(done);
        if (busy) return;
        busy = true;

        if (!string.IsNullOrEmpty(refreshToken)) Refresh();
        else SignUp();
    }

    // The server said the token is no good: get a new one next time
    public void Invalidate()
    {
        expiresAt = DateTime.MinValue;
    }

    // Forgets this device's account (the next sign-in creates a new player). For testing.
    public void ForgetAccount()
    {
        refreshToken = "";
        UserId = "";
        idToken = "";
        expiresAt = DateTime.MinValue;
        PlayerPrefs.DeleteKey(RefreshPref);
        PlayerPrefs.DeleteKey(UserPref);
        PlayerPrefs.Save();
    }

    private void SignUp()
    {
        http.Post(config.SignUpUrl, "{\"returnSecureToken\":true}", "application/json", null, result =>
        {
            if (result.networkError || result.status == 0) { Finish(Failed("Could not reach the sign-in service.", true)); return; }
            SignUpResponse parsed = result.status == 200 ? Parse<SignUpResponse>(result.body) : null;
            if (parsed == null || string.IsNullOrEmpty(parsed.idToken)) { Finish(Failed("Sign-in was refused: " + ErrorOf(result.body), false)); return; }
            Store(parsed.idToken, parsed.refreshToken, parsed.expiresIn, parsed.localId);
            Finish(BackendResult.Ok());
        });
    }

    private void Refresh()
    {
        string body = "grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(refreshToken);
        http.Post(config.RefreshUrl, body, "application/x-www-form-urlencoded", null, result =>
        {
            if (result.networkError || result.status == 0) { Finish(Failed("Could not reach the sign-in service.", true)); return; }
            RefreshResponse parsed = result.status == 200 ? Parse<RefreshResponse>(result.body) : null;
            if (parsed == null || string.IsNullOrEmpty(parsed.id_token))
            {
                // The saved account is no longer valid (deleted, or from another project): start a new one
                refreshToken = "";
                SignUp();
                return;
            }
            Store(parsed.id_token, parsed.refresh_token, parsed.expires_in, parsed.user_id);
            Finish(BackendResult.Ok());
        });
    }

    private void Store(string token, string refresh, string expiresInSeconds, string userId)
    {
        idToken = token;
        if (!string.IsNullOrEmpty(refresh)) refreshToken = refresh;
        if (!string.IsNullOrEmpty(userId)) UserId = userId;
        int seconds = 3600;
        int.TryParse(expiresInSeconds, out seconds);
        expiresAt = now() + TimeSpan.FromSeconds(seconds > 0 ? seconds : 3600);

        PlayerPrefs.SetString(RefreshPref, refreshToken ?? "");
        PlayerPrefs.SetString(UserPref, UserId ?? "");
        PlayerPrefs.Save();
    }

    private void Finish(BackendResult result)
    {
        busy = false;
        List<Action<BackendResult>> callbacks = new List<Action<BackendResult>>(waiting);
        waiting.Clear();
        foreach (Action<BackendResult> callback in callbacks) callback(result);
    }

    private static BackendResult Failed(string message, bool offline)
    {
        return new BackendResult { ok = false, error = message, offline = offline };
    }

    private static T Parse<T>(string json) where T : class
    {
        try { return JsonUtility.FromJson<T>(json); }
        catch { return null; }
    }

    private static string ErrorOf(string body)
    {
        string error = JsonExtract.Value(body, "error");
        if (error == null) return "no details";
        string message = JsonExtract.Value(error, "message");
        return message != null ? message.Trim('"') : error;
    }
}
