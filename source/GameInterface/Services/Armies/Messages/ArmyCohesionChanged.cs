using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Local event raised on the server when an armies' cohesion changes, so it can be replicated to clients.
/// </summary>
public readonly struct ArmyCohesionChanged : IEvent
{
    public readonly Army Army;
    public readonly float Cohesion;

    public ArmyCohesionChanged(Army army, float cohesion)
    {
        Army = army;
        Cohesion = cohesion;
    }
}
