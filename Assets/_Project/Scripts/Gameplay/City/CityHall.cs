using UnityEngine;

public class CityHall : MonoBehaviour, IBuildingClickHandler
{
    public void OnBuildingClicked()
    {
        Debug.Log("Clicked on City Hall");
    }
}
