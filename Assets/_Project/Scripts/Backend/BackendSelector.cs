using UnityEngine;

// Picks the backend when the game starts: the Firebase one if there is a FirebaseConfig.json (see docs/FIREBASE_SETUP.md)
// and it has not been switched off, otherwise the local one. The Cheats menu can switch it (takes effect next start).
public static class BackendSelector
{
    public const string Preference = "UseLocalBackend"; // 1 = stay local even though Firebase is configured

    public static bool FirebaseConfigured => FirebaseConfig.Load() != null;
    public static bool WantsLocal => PlayerPrefs.GetInt(Preference, 0) == 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Choose()
    {
        FirebaseConfig config = FirebaseConfig.Load();
        if (config != null && !WantsLocal)
        {
            Backend.Current = new FirebaseBackend(config, new UnityHttpClient());
            Debug.Log("Backend: " + Backend.Current.Name + " (" + config.projectId + ")");
        }
        else
        {
            Backend.Current = new LocalBackend();
            Debug.Log("Backend: local" + (config != null ? " (Firebase is configured but switched off)" : " (no FirebaseConfig.json)"));
        }
    }

    public static void SetLocal(bool local)
    {
        PlayerPrefs.SetInt(Preference, local ? 1 : 0);
        PlayerPrefs.Save();
    }
}
