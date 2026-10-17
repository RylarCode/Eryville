using System;
using UnityEngine;

// Shows a character's animations on its body: walking (happy / ordinary / sad by mood, faster or slower with its speed,
// mirrored to face the way it walks), standing, and the one-off animations for going into and coming out of a building.
// Added to the body by Character when the character's type has a CharacterAnimationSet; without one nothing changes.
public class CharacterAnimator : MonoBehaviour
{
    private Character character;
    private CharacterMovement movement;
    private SpriteRenderer spriteRenderer;
    private CharacterAnimationSet set;
    private Sprite stillSprite;
    private readonly AnimationPlayer player = new AnimationPlayer();

    public AnimationPlayer Player => player;

    public void Setup(Character character, CharacterMovement movement, SpriteRenderer spriteRenderer, CharacterAnimationSet set, Sprite stillSprite)
    {
        this.character = character;
        this.movement = movement;
        this.spriteRenderer = spriteRenderer;
        this.set = set;
        this.stillSprite = stillSprite;
    }

    // Whether the character has something to show for this (so the caller can wait for it, or skip it)
    public bool Has(AnimKey key)
    {
        return set != null && set.Resolve(key) != null;
    }

    // Plays the animation once and calls done when it has finished (at once if there is none)
    public void PlayOnce(AnimKey key, Action done)
    {
        player.PlayOnce(set != null ? set.Resolve(key) : null, done);
    }

    public void CancelOneShot()
    {
        player.CancelOneShot();
    }

    private void Update()
    {
        if (set == null || spriteRenderer == null || !spriteRenderer.enabled) return; // out of sight: nothing to draw

        bool moving = movement != null && movement.IsMoving;
        if (!player.OneShotRunning)
        {
            float mood = character != null && character.Owned != null ? character.Owned.Mood : 50f;
            player.Play(set.Resolve(moving ? set.WalkFor(mood) : AnimKey.Idle));
        }

        // Walking faster (on a road, or in a good mood) plays the walk faster too
        float rate = 1f;
        if (moving && character != null && character.Owned != null && character.Owned.Type.moveSpeed > 0f && !player.OneShotRunning)
        {
            rate = Mathf.Clamp(movement.EffectiveSpeed / character.Owned.Type.moveSpeed, 0.5f, 2f);
        }

        Sprite frame = player.Tick(Time.deltaTime, rate);
        spriteRenderer.sprite = frame != null ? frame : stillSprite;

        if (moving && Mathf.Abs(movement.Direction.x) > 0.01f)
        {
            spriteRenderer.flipX = (movement.Direction.x < 0f) == set.facesRight;
        }
    }
}
