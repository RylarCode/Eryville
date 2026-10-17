using System;
using UnityEngine;

// Which Firebase project the game talks to. Lives in Assets/_Project/Resources/FirebaseConfig.json (see docs/FIREBASE_SETUP.md).
// None of this is secret: the API key only says which project the request is for (it is in every app that uses Firebase).
// Without the file, the game uses the local backend.
[Serializable]
public class FirebaseConfig
{
    public string projectId;
    public string apiKey;
    public string region = "us-central1";   // where the functions run (must match functions/src/index.ts)
    public bool useEmulator;                // talk to the Firebase emulators on this computer instead of the real project
    public string emulatorHost = "127.0.0.1";
    public int authPort = 9099;
    public int functionsPort = 5001;

    public bool IsValid => !string.IsNullOrEmpty(projectId) && (!string.IsNullOrEmpty(apiKey) || useEmulator);

    private string AuthBase => useEmulator ? $"http://{emulatorHost}:{authPort}/" : "https://";

    // Anonymous sign-in: creates a new player account
    public string SignUpUrl => AuthBase + $"identitytoolkit.googleapis.com/v1/accounts:signUp?key={apiKey}";

    // Trades a refresh token for a new id token
    public string RefreshUrl => AuthBase + $"securetoken.googleapis.com/v1/token?key={apiKey}";

    // The address of a callable function
    public string FunctionUrl(string name)
    {
        return useEmulator
            ? $"http://{emulatorHost}:{functionsPort}/{projectId}/{region}/{name}"
            : $"https://{region}-{projectId}.cloudfunctions.net/{name}";
    }

    // The config from Resources/FirebaseConfig.json, or null if there is none (or it is not filled in)
    public static FirebaseConfig Load()
    {
        TextAsset file = Resources.Load<TextAsset>("FirebaseConfig");
        if (file == null) return null;

        try
        {
            FirebaseConfig config = JsonUtility.FromJson<FirebaseConfig>(file.text);
            return config != null && config.IsValid ? config : null;
        }
        catch (Exception e)
        {
            Debug.LogError("FirebaseConfig.json is not valid: " + e.Message);
            return null;
        }
    }
}
