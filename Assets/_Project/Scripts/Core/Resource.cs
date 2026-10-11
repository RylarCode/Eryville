using UnityEngine;

[System.Serializable]
public class Resource
{
    public string resourceName;
    public float currentAmount;
    public float growthPerSecond;

    public Resource(string name, float startingAmount = 0, float growthRate = 0)
    {
        resourceName = name;
        currentAmount = startingAmount;
        growthPerSecond = growthRate;
    }
}
