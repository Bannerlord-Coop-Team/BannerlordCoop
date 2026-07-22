using Common.Messaging;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEnginesConstructionProgress.Messages;

/// <summary>
/// A siege engine's construction or redeployment progress crossed a sync threshold on the server.
/// </summary>
public readonly struct SiegeEngineProgressChanged : IEvent
{
    public readonly SiegeEngineConstructionProgress SiegeEngine;
    public readonly bool IsRedeployment;
    public readonly float Value;

    public SiegeEngineProgressChanged(SiegeEngineConstructionProgress siegeEngine, bool isRedeployment, float value)
    {
        SiegeEngine = siegeEngine;
        IsRedeployment = isRedeployment;
        Value = value;
    }
}
