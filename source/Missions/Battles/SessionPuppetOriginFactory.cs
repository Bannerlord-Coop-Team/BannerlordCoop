using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>Puppet origins for a mission session with no campaign: no party to wait for.</summary>
public class SessionPuppetOriginFactory : IPuppetOriginFactory
{
    public bool TryCreate(BasicCharacterObject character, BattleAgentSpawnData data, Team team, out IAgentOriginBase origin)
    {
        origin = new SessionPuppetAgentOrigin(character, team, new UniqueTroopDescriptor(data.TroopSeed));
        return true;
    }
}
