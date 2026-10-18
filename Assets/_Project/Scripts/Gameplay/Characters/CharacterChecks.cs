using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Looks over the character types for the mistakes that are easy to make when adding one (see docs/NEW_CHARACTER.md).
// Pure code: the editor menu "Eryville > Check Characters" prints what it finds, and tests can call it too.
public static class CharacterChecks
{
    // allTypes: every CharacterType asset in the project. inGame: the ones listed on the CharacterManager (what can be hired).
    // Each problem is one readable sentence naming the character.
    public static List<string> Problems(IReadOnlyList<CharacterType> allTypes, IReadOnlyList<CharacterType> inGame)
    {
        List<string> problems = new List<string>();
        Dictionary<int, CharacterType> byId = new Dictionary<int, CharacterType>();

        foreach (CharacterType type in allTypes)
        {
            if (type == null) continue;
            string who = string.IsNullOrEmpty(type.displayName) ? $"\"{type.name}\"" : $"{type.displayName} (\"{type.name}\")";

            if (string.IsNullOrEmpty(type.displayName)) problems.Add($"{who} has no display name.");

            if (byId.TryGetValue(type.typeId, out CharacterType other))
                problems.Add($"{who} has the same typeId ({type.typeId}) as \"{other.name}\": saved games and the server would mix them up. Give each character its own number.");
            else
                byId[type.typeId] = type;

            if (type.characterPrefab == null) problems.Add($"{who} has no character prefab, so it cannot appear in the world.");
            else if (type.characterPrefab.GetComponent<Character>() == null) problems.Add($"{who}: its prefab has no Character component.");

            if (type.sprite == null) problems.Add($"{who} has no sprite (the small picture used in the world and as the fallback for everything else).");
            if (type.portrait == null) problems.Add($"{who} has no portrait; the small sprite is shown on the recruit screen instead.");
            if (type.face == null) problems.Add($"{who} has no face icon; the portrait or sprite is used in lists instead.");

            if (type.animations != null)
            {
                CharacterAnimationSet set = type.animations;
                if (set.Resolve(AnimKey.Walk, Facing.Down) == null) problems.Add($"{who}: the animation set has no walk or idle frames, so the character will not animate.");
                else
                {
                    foreach (Facing facing in new[] { Facing.Down, Facing.Up, Facing.Left, Facing.Right })
                    {
                        if (set.walk.Get(facing).IsEmpty) problems.Add($"{who}: the walk has no {facing.ToString().ToLowerInvariant()} frames; another direction is shown instead.");
                    }
                }
            }

            if (type.favouriteFood != null && (!type.favouriteFood.edible || type.favouriteFood.nourishment <= 0f)) problems.Add($"{who}: its favourite food ({type.favouriteFood.Name}) is not edible, so it will never be eaten.");
            if (type.specialties != null)
            {
                foreach (Specialty specialty in type.specialties)
                {
                    if (specialty == null || string.IsNullOrEmpty(specialty.tag)) problems.Add($"{who} has a specialty with no tag, so it does nothing.");
                    else if (specialty.multiplier <= 0f) problems.Add($"{who}: the {specialty.tag} specialty has a multiplier of 0 or less, which would stop it working.");
                }
            }
            if (type.appetite <= 0f) problems.Add($"{who} has an appetite of 0 or less (it is treated as 1).");

            if (type.defaultSchedule == null || type.defaultSchedule.Count == 0) problems.Add($"{who} has no default schedule, so it will just stand around.");
            if (inGame != null && !inGame.Contains(type)) problems.Add($"{who} is not in the CharacterManager's list (GameScene), so it cannot be hired.");
        }
        return problems;
    }
}
