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
    public int InflictedDamage { get; }
    public BoneBodyPartType VictimBodyPart { get; }
    public int DeathAction { get; }

    public BattleAgentDied(
        Agent agent,
        Agent affectorAgent,
        bool wounded,
        int inflictedDamage = 0,
        BoneBodyPartType victimBodyPart = default,
        int deathAction = -1)
    {
        Agent = agent;
        AffectorAgent = affectorAgent;
        Wounded = wounded;
        InflictedDamage = inflictedDamage;
        VictimBodyPart = victimBodyPart;
        DeathAction = deathAction;
    }
}
