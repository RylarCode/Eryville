using System;
using UnityEngine;

// Plays SpriteAnimations: works out which picture to show at each moment. No Unity components, so it can be tested exactly.
// An animation either loops (walking) or plays once and then reports it is done (entering a building).
public class AnimationPlayer
{
    private SpriteAnimation current;
    private float time;
    private bool oneShot;
    private Action whenDone;

    public SpriteAnimation Current => current;
    public bool OneShotRunning => oneShot;

    // Shows this animation from now on, keeping its place if it is the one already playing
    // (so a walk does not restart every frame). Does nothing while a one-shot is playing.
    public void Play(SpriteAnimation animation)
    {
        if (oneShot || animation == current) return;
        current = animation;
        time = 0f;
    }

    // Plays an animation once, then calls whenDone. With no animation (or an empty one) whenDone is called straight away.
    public void PlayOnce(SpriteAnimation animation, Action done)
    {
        if (animation == null || animation.IsEmpty)
        {
            done?.Invoke();
            return;
        }
        current = animation;
        time = 0f;
        oneShot = true;
        whenDone = done;
    }

    // Stops a one-shot without calling its callback (what it was waiting to do no longer applies)
    public void CancelOneShot()
    {
        oneShot = false;
        whenDone = null;
    }

    // Moves time on and returns the picture to show (null when there is no animation). rate speeds playback up or down.
    public Sprite Tick(float seconds, float rate = 1f)
    {
        if (current == null || current.IsEmpty) return null;

        time += seconds * rate;
        int count = current.frames.Length;
        int index = (int)(time * current.framesPerSecond);

        if (oneShot)
        {
            if (time >= current.Duration)
            {
                Action callback = whenDone;
                oneShot = false;
                whenDone = null;
                callback?.Invoke();
                return current != null && !current.IsEmpty ? current.frames[count - 1] : null;
            }
            return current.frames[Mathf.Clamp(index, 0, count - 1)];
        }

        if (!current.loop) return current.frames[Mathf.Clamp(index, 0, count - 1)];
        return current.frames[((index % count) + count) % count];
    }
}
