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
    Eat,            // eating a meal (plays once, when standing still)
}

// One animation drawn four times, once for each way the character can face. Nothing is mirrored: each direction has its own pictures.
[Serializable]
public class DirectionalAnimation
{
    public SpriteAnimation down = new SpriteAnimation();
    public SpriteAnimation up = new SpriteAnimation();
    public SpriteAnimation left = new SpriteAnimation();
    public SpriteAnimation right = new SpriteAnimation();

    public DirectionalAnimation() { }

    // For one-off animations (entering / leaving a building), which play once instead of looping
    public DirectionalAnimation(bool loop)
    {
        down.loop = up.loop = left.loop = right.loop = loop;
    }

    // Exactly this direction (may be empty)
    public SpriteAnimation Get(Facing facing)
    {
        switch (facing)
        {
            case Facing.Up: return up;
            case Facing.Left: return left;
            case Facing.Right: return right;
            default: return down;
        }
    }

    // This direction, or if it was not drawn the first one that was (down, up, left, right); null if nothing is drawn at all
    public SpriteAnimation Resolve(Facing facing)
    {
        SpriteAnimation exact = Get(facing);
        if (exact != null && !exact.IsEmpty) return exact;
        foreach (Facing other in AllFacings)
        {
            SpriteAnimation candidate = Get(other);
            if (candidate != null && !candidate.IsEmpty) return candidate;
        }
        return null;
    }

    public bool IsEmpty => Resolve(Facing.Down) == null;

    private static readonly Facing[] AllFacings = { Facing.Down, Facing.Up, Facing.Left, Facing.Right };
}

// All the animations of one character, each in four directions. Every one is optional: a missing one falls back to a
// plainer one (WalkHappy and WalkSad to Walk, Walk to Idle), a missing direction to a direction that was drawn, and with
// nothing at all the character keeps its still picture.
// Give each character type its own set. The quickest way to fill one in is "Fill from sprites" (right-click the asset
// in the Project window), which reads the names of a sliced sprite sheet: see docs/NEW_CHARACTER.md.
[CreateAssetMenu(fileName = "NewCharacterAnimations", menuName = "Scriptable Objects/Character Animations")]
public class CharacterAnimationSet : ScriptableObject
{
    public DirectionalAnimation idle = new DirectionalAnimation();
    public DirectionalAnimation walk = new DirectionalAnimation();
    public DirectionalAnimation walkHappy = new DirectionalAnimation();
    public DirectionalAnimation walkSad = new DirectionalAnimation();
    public DirectionalAnimation enterBuilding = new DirectionalAnimation(false);
    public DirectionalAnimation exitBuilding = new DirectionalAnimation(false);
    public DirectionalAnimation eat = new DirectionalAnimation(false);

    [Header("Mood")]
    public float happyMood = 70f;   // at or above this the character walks happily
    public float sadMood = 30f;     // at or below this the character walks sadly

    // The four-direction animation made for this exact situation (may be empty)
    public DirectionalAnimation Get(AnimKey key)
    {
        switch (key)
        {
            case AnimKey.Walk: return walk;
            case AnimKey.WalkHappy: return walkHappy;
            case AnimKey.WalkSad: return walkSad;
            case AnimKey.EnterBuilding: return enterBuilding;
            case AnimKey.ExitBuilding: return exitBuilding;
            case AnimKey.Eat: return eat;
            default: return idle;
        }
    }

    // The animation to show for this situation while facing this way, falling back to a plainer one; null if there is none
    public SpriteAnimation Resolve(AnimKey key, Facing facing)
    {
        DirectionalAnimation exact = Get(key);
        if (exact != null && !exact.IsEmpty) return exact.Resolve(facing);

        switch (key)
        {
            case AnimKey.WalkHappy:
            case AnimKey.WalkSad:
                return Resolve(AnimKey.Walk, facing);
            case AnimKey.Walk:
                return Resolve(AnimKey.Idle, facing);
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
