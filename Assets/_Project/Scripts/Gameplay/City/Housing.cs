using UnityEngine;

// Behaviour for buildings that house characters. Capacity comes from BuildingType.housingSlots
// and the residents list lives on Building; this adds what is specific to homes.
[RequireComponent(typeof(Building))]
public class Housing : MonoBehaviour, IBuildingComponent
{
    public void OnBuildingInitialized(Building building)
    {
        // Fill the new home with homeless characters, up to its housing capacity
        while (building.HasHousingSlot)
        {
            Character homeless = CharacterManager.Instance.getHomeless();
            if (homeless == null || !CharacterManager.Instance.assignCharacterHouse(homeless, building)) break;
        }
    }
}
