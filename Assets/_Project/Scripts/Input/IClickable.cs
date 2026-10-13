// Implemented by anything in the world the player can click on (characters, buildings...).
// InputManager finds it on whatever was hit and calls OnClicked, without knowing the concrete type.
public interface IClickable
{
    // When several clickable things overlap under the cursor, the highest priority gets the click
    // (characters are above the buildings they stand on).
    int ClickPriority { get; }

    void OnClicked();
}
