// Implemented by anything in the world the player can click on (characters, buildings...).
// InputManager finds it on whatever was hit and calls OnClicked, without knowing the concrete type.
public interface IClickable
{
    void OnClicked();
}
