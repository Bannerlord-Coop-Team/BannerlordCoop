using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Local event raised when an authoritative agent is removed as killed or unconscious in a coop field battle.
/// </summary>
public record BattleAgentDied : IEvent
{
    public Agent Agent { get; }
    public Agent AffectorAgent { get; }

    /// <summary>True when the agent fell unconscious (wounded) rather than being killed outright.</summary>
    public bool Wounded { get; }
    public KillingBlow KillingBlow { get; }

    public BattleAgentDied(Agent agent, Agent affectorAgent, bool wounded, KillingBlow killingBlow)
    {
        Agent = agent;
        AffectorAgent = affectorAgent;
        Wounded = wounded;
        KillingBlow = killingBlow;
    }
}
