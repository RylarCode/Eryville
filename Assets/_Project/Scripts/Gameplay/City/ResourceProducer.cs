using UnityEngine;

// Makes a building produce gold / wood. Base rates come from the BuildingType. A building that has
// worker slots only produces while at least one assigned worker is physically on site (not while they
// are walking there), and every such worker multiplies the output by their productivity.
// Stateless: rates are computed from the building's current workers each time they are read.
[RequireComponent(typeof(Building))]
public class ResourceProducer : MonoBehaviour
{
    private Building building;

    private void Awake()
    {
        building = GetComponent<Building>();
    }

    // Whether the building is producing right now. A building with no worker slots cannot be staffed, so it just runs.
    public bool IsRunning => building.WorkerSlots <= 0 || building.WorkersOnSite.Count > 0;

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

    public float GoldPerSec => building.buildingType != null && IsRunning ? building.buildingType.goldGenerationPerSec * WorkerMultiplier : 0f;
    public float WoodPerSec => building.buildingType != null && IsRunning ? building.buildingType.woodGenerationPerSec * WorkerMultiplier : 0f;
}
