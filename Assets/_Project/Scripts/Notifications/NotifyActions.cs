using System;
using System.Linq;
using UnityEngine;

// What tapping a message can do: look at the thing it is about. Pass the result as Notify.Show's onTap.
// Each checks that the thing still exists when it is tapped (it may have been removed since).
public static class NotifyActions
{
    public static Action ForBuilding(Building building)
    {
        return () =>
        {
            if (building == null) return;
            if (UIManager.Instance != null) UIManager.Instance.ShowBuildingDetails(building);
            Focus(building.transform.position);
        };
    }

    public static Action ForCharacter(OwnedCharacter character)
    {
        return () =>
        {
            if (character == null || !CharacterManager.Instance.Roster.Contains(character)) return;
            if (UIManager.Instance != null) UIManager.Instance.ShowCharacterDetails(character);
            if (character.HasAvatar) Focus(character.Avatar.transform.position);
        };
    }

    public static Action ForPosition(Vector3 world)
    {
        return () => Focus(world);
    }

    // Looks at a place. On a phone the details sheet covers the lower part of the screen, so the target goes higher up.
    public static void Focus(Vector3 world)
    {
        if (CameraController.Instance == null) return;
        bool sheet = UILayout.Instance != null && UILayout.Instance.IsPortrait;
        CameraController.Instance.FocusOn(world, sheet ? 0.2f : 0f);
    }
}
