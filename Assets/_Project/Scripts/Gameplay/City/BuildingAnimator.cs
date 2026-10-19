using UnityEngine;

// Shows a building's closed / open animations on its picture, and the short change between them.
// Added to the building by Building when its type has a BuildingAnimationSet; without one nothing changes.
public class BuildingAnimator : MonoBehaviour
{
    private Building building;
    private SpriteRenderer spriteRenderer;
    private BuildingAnimationSet set;
    private Sprite stillSprite;
    private readonly AnimationPlayer player = new AnimationPlayer();
    private bool? wasOpen;

    public AnimationPlayer Player => player;
    public bool IsOpen => wasOpen == true;

    public void Setup(Building building, SpriteRenderer spriteRenderer, BuildingAnimationSet set, Sprite stillSprite)
    {
        this.building = building;
        this.spriteRenderer = spriteRenderer;
        this.set = set;
        this.stillSprite = stillSprite;
    }

    private bool ComputeOpen()
    {
        BuildingType type = building.buildingType;
        bool night = GameClock.Instance != null && GameClock.Instance.Current == TimeOfDay.Night;
        return BuildingOpenState.IsOpen(type.openRule, type.workerSlots, building.WorkersOnSite.Count, type.housingSlots, building.RestingInside, night);
    }

    private void Update()
    {
        if (set == null || spriteRenderer == null || building == null || building.buildingType == null) return;

        bool open = ComputeOpen();
        if (wasOpen.HasValue && wasOpen.Value != open)
        {
            // It just opened or closed: play the change once (if there is one), then settle into the new state
            player.CancelOneShot();
            player.PlayOnce(set.Resolve(open ? BuildingAnimKey.Opening : BuildingAnimKey.Closing), null);
        }
        wasOpen = open;

        player.Play(set.Resolve(open ? BuildingAnimKey.Open : BuildingAnimKey.Closed));
        Sprite frame = player.Tick(Time.deltaTime);
        if (building.ShowingTopDownArt) return; // the flat view shows its own picture
        spriteRenderer.sprite = frame != null ? frame : stillSprite;
    }
}
