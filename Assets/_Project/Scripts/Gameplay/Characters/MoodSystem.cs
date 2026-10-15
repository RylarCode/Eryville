using System.Collections.Generic;
using UnityEngine;

// One reason a character's mood goes up or down. The MoodSystem adds up what every rule says, per second.
// New reasons (a story state like being in love, how much they like their workplace...) are new rules:
// write a class that implements this and give it to MoodSystem.AddRule. Nothing else needs to change.
public interface IMoodRule
{
    // How much this rule changes the character's mood per second right now (negative drains it, 0 = no effect)
    float ChangePerSecond(OwnedCharacter character);
}

// Moves every owned character's mood once a second, according to its rules, while the game is running
// (not while a menu has it paused). The numbers come from each character type's MoodProfile.
public class MoodSystem : MonoBehaviour
{
    public static MoodSystem Instance { get; private set; }

    [SerializeField] private float tickSeconds = 1f;

    private readonly List<IMoodRule> rules = new List<IMoodRule>();
    private float elapsed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        rules.Add(new HomelessMoodRule());
        rules.Add(new RestingMoodRule());
        rules.Add(new TravelMoodRule());
        rules.Add(new WorldStateMoodRule());
    }

    public void AddRule(IMoodRule rule) { if (rule != null && !rules.Contains(rule)) rules.Add(rule); }
    public void RemoveRule(IMoodRule rule) { rules.Remove(rule); }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.currentGameState != GameManager.gameState.Normal) return;
        if (CharacterManager.Instance == null) return;

        elapsed += Time.deltaTime;
        if (elapsed < tickSeconds) return;
        float seconds = elapsed;
        elapsed = 0f;

        foreach (OwnedCharacter character in CharacterManager.Instance.Roster)
        {
            float change = 0f;
            foreach (IMoodRule rule in rules)
            {
                float effect = rule.ChangePerSecond(character);
                // Self-awareness buffs: gains scale by MoodRecovery, losses by MoodDrain
                effect *= effect > 0f ? character.BuffMultiplier(BuffStat.MoodRecovery) : character.BuffMultiplier(BuffStat.MoodDrain);
                change += effect;
            }
            if (change != 0f) character.Mood += change * seconds;
        }
    }

    // --- The rules that exist now ---

    // Having nowhere to live wears people down
    private sealed class HomelessMoodRule : IMoodRule
    {
        public float ChangePerSecond(OwnedCharacter character)
        {
            return character.HomeBuilding == null ? -character.Type.mood.homelessDrainPerSecond : 0f;
        }
    }

    // Resting, at home or somewhere for leisure, restores it
    private sealed class RestingMoodRule : IMoodRule
    {
        public float ChangePerSecond(OwnedCharacter character)
        {
            return character.HasAvatar && character.Avatar.CurrentActivity == Character.Activity.Resting
                ? character.Type.mood.restRecoveryPerSecond : 0f;
        }
    }

    // Getting around costs a little each second: the longer the trip, the more (which rewards living near work)
    private sealed class TravelMoodRule : IMoodRule
    {
        public float ChangePerSecond(OwnedCharacter character)
        {
            return character.HasAvatar && character.Avatar.CurrentActivity == Character.Activity.Traveling
                ? -character.Type.mood.travelDrainPerSecond : 0f;
        }
    }

    // The state of the world (war...) weighs on everyone, by whatever the character type says
    private sealed class WorldStateMoodRule : IMoodRule
    {
        public float ChangePerSecond(OwnedCharacter character)
        {
            return GameManager.Instance != null ? character.Type.mood.WorldStateChange(GameManager.Instance.worldState) : 0f;
        }
    }
}
