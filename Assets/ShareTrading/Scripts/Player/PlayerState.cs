using UnityEngine;

public class PlayerState
{
    public double cash { get; internal set; }
    public float manPower { get; internal set; }
    public float information { get; internal set; }
    public float reputation { get; internal set; }

    public PlayerState(
        double initialCash,
        float initialManPower, 
        float initialInformation, 
        float initialReputation)
    {
        cash = initialCash;
        manPower = initialManPower;
        information = initialInformation;
        reputation = initialReputation;
    }

    public void addCash(double value)
    {
        cash += value;
    }

    public void addManPower(float value)
    {
        manPower += value;
    }

    public void addInformation(float value)
    {
        information += value;
    }

    public void addReputation(float value)
    {
        reputation += value;
    }
}
