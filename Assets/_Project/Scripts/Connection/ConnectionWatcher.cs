using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Keeps an eye on everyone's Connection while the game runs:
//  - when a character's chapter becomes playable (its loyalty reached the threshold, a chapter or quest it waited for is done...) it
//    says so: "Baker has something to share" (tap: open their Connection);
//  - when the story or the quests change, characters pick up the buffs their Connection gives (or loses).
// What is already playable when a game is loaded is not announced again. Added by StoryManager.
public class ConnectionWatcher : MonoBehaviour
{
    public const float CheckSeconds = 0.5f;

    private readonly HashSet<string> announced = new HashSet<string>();
    private bool primed;
    private string buffStamp;
    private float nextCheck;

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetAnnouncements();
    }

    // Forget what was announced; the next look takes what is playable then as already known (used when a game is loaded)
    public void ResetAnnouncements()
    {
        announced.Clear();
        primed = false;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + CheckSeconds;
        if (GameManager.Instance == null || CharacterManager.Instance == null) return;
        if (GameSession.Instance != null && !GameSession.Instance.Ready) return;
        Check();
    }

    public void Check()
    {
        // Buffs: when the story or quests changed, everyone recalculates what their buffs do (speed now, the rest when next used)
        string stamp = ConnectionBuffs.Stamp;
        if (stamp != buffStamp)
        {
            buffStamp = stamp;
            foreach (OwnedCharacter owned in CharacterManager.Instance.Roster) owned.RefreshBuffs();
        }

        foreach (OwnedCharacter owned in CharacterManager.Instance.Roster)
        {
            foreach (ConnectionItem item in Connection.ItemsFor(owned.Type))
            {
                if (item.Chapter == null || Connection.StateOf(item) != ConnectionState.Available) continue; // quests announce themselves
                string key = owned.Type.typeId + ":" + item.Key;
                if (!announced.Add(key) || !primed) continue;
                OwnedCharacter who = owned;
                Notify.Show($"{owned.DisplayName} has something to share", NotifyKind.Info, owned.Type.FaceOrSprite,
                    () => { if (UIManager.Instance != null) UIManager.Instance.OpenConnection(who); }, "connection:" + key);
            }
        }
        primed = true;
    }
}
