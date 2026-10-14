// Implemented by anything that turns the player's clicks into building something (placing a building,
// moving one, painting roads...). While any of them is active, clicks go to it and do not select
// whatever happens to be under the cursor.
public interface IPlacementMode
{
    bool IsPlacing { get; }
}
