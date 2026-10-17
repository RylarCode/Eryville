using System;
using UnityEngine;

// A flip-book: pictures shown one after another. Used for the small character in the world.
[Serializable]
public class SpriteAnimation
{
    public Sprite[] frames = new Sprite[0];
    public float framesPerSecond = 8f;
    public bool loop = true;                 // false: plays once and stops on the last picture (entering / leaving a building)

    public bool IsEmpty => frames == null || frames.Length == 0;
    public float Duration => IsEmpty || framesPerSecond <= 0f ? 0f : frames.Length / framesPerSecond;
}

// What a character can be seen doing
public enum AnimKey
{
    Idle,           // standing about
    Walk,           // walking in an ordinary mood
    WalkHappy,      // walking in a good mood
    WalkSad,        // walking in a bad mood
    EnterBuilding,  // going in (plays once, at the door)
    ExitBuilding,   // coming out (plays once, at the door, before walking off)
}

// All the animations of one character. Every one is optional: a missing one falls back to a plainer one
// (WalkHappy and WalkSad to Walk, Walk to Idle), and with nothing at all the character keeps its still picture.
// Give each character type its own set (or share one between similar characters).
[CreateAssetMenu(fileName = "NewCharacterAnimations", menuName = "Scriptable Objects/Character Animations")]
public class CharacterAnimationSet : ScriptableObject
{
    public SpriteAnimation idle = new SpriteAnimation();
    public SpriteAnimation walk = new SpriteAnimation();
    public SpriteAnimation walkHappy = new SpriteAnimation();
    public SpriteAnimation walkSad = new SpriteAnimation();
    public SpriteAnimation enterBuilding = new SpriteAnimation { loop = false };
    public SpriteAnimation exitBuilding = new SpriteAnimation { loop = false };

    [Header("Mood")]
    public float happyMood = 70f;   // at or above this the character walks happily
    public float sadMood = 30f;     // at or below this the character walks sadly

    [Header("Drawing")]
    public bool facesRight = true;  // which way the pictures look; the character is mirrored when it walks the other way

    // The animation made for this exact situation (may be empty)
    public SpriteAnimation Get(AnimKey key)
    {
        switch (key)
        {
            case AnimKey.Walk: return walk;
            case AnimKey.WalkHappy: return walkHappy;
            case AnimKey.WalkSad: return walkSad;
            case AnimKey.EnterBuilding: return enterBuilding;
            case AnimKey.ExitBuilding: return exitBuilding;
            default: return idle;
        }
    }

    // The animation to show for this situation, falling back to a plainer one; null if there is none
    public SpriteAnimation Resolve(AnimKey key)
    {
        SpriteAnimation exact = Get(key);
        if (exact != null && !exact.IsEmpty) return exact;

        switch (key)
        {
            case AnimKey.WalkHappy:
            case AnimKey.WalkSad:
                return Resolve(AnimKey.Walk);
            case AnimKey.Walk:
                return Resolve(AnimKey.Idle);
            default:
                return null;
        }
    }

    // Which walk to use for this mood
    public AnimKey WalkFor(float mood)
    {
        if (mood >= happyMood) return AnimKey.WalkHappy;
        if (mood <= sadMood) return AnimKey.WalkSad;
        return AnimKey.Walk;
    }
}
