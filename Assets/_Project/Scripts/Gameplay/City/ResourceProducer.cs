using UnityEngine;

// Makes a building produce gold / wood. Base rates come from the BuildingType; every assigned
// worker who is physically on site multiplies the output by their productivity.
// Stateless: rates are computed from the building's current workers each time they are read.
[RequireComponent(typeof(Building))]
public class ResourceProducer : MonoBehaviour
{
    private Building building;

    private void Awake()
    {
        building = GetComponent<Building>();
    }

    public float WorkerMultiplier
    {
        get
        {
            float multiplier = 1f; // Start at 1 because it's multiplication
            foreach (Character worker in building.WorkersOnSite)
            {
                multiplier *= worker.Productivity;
            }
            return multiplier;
        }
    }

    public float GoldPerSec => building.buildingType != null ? building.buildingType.goldGenerationPerSec * WorkerMultiplier : 0f;
    public float WoodPerSec => building.buildingType != null ? building.buildingType.woodGenerationPerSec * WorkerMultiplier : 0f;
}
