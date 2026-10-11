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
        Character potentialHomeless = CharacterManager.Instance.getHomeless();
        if (potentialHomeless != null)
        {
            potentialHomeless.AssignedHome(this.gameObject.GetComponent<Building>());
            this.gameObject.GetComponent<Building>().registerCharacter(potentialHomeless);
        }
    }

}
