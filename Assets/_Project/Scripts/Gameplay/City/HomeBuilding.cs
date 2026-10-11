using UnityEngine;

public class HomeBuilding : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Initialize(BuildingType type)
    {
        // Fill the new home with homeless characters, up to its housing capacity
        Building building = GetComponent<Building>();
        while (building.HasHousingSlot)
        {
            Character homeless = CharacterManager.Instance.getHomeless();
            if (homeless == null || !CharacterManager.Instance.assignCharacterHouse(homeless, building)) break;
        }
    }

}
