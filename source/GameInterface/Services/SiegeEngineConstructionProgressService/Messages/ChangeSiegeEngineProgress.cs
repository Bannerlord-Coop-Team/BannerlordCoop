using Common.Messaging;

namespace GameInterface.Services.SiegeEnginesConstructionProgress.Messages;

/// <summary>
/// Has GameInterface apply a siege engine construction or redeployment progress value on this client.
/// </summary>
public record ChangeSiegeEngineProgress : ICommand
{
    public string SiegeEngineId { get; }
    public bool IsRedeployment { get; }
    public float Value { get; }

    public ChangeSiegeEngineProgress(string siegeEngineId, bool isRedeployment, float value)
    {
        SiegeEngineId = siegeEngineId;
        IsRedeployment = isRedeployment;
        Value = value;
    }
}
