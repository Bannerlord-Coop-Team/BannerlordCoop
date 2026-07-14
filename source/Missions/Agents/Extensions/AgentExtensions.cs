using GameInterface;
using GameInterface.Services.MapEvents;
using System.Runtime.CompilerServices;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Extensions;

public static class AgentExtensions
{
    private static readonly ConditionalWeakTable<Agent, AgentExtendedData> agentAdditions = new ConditionalWeakTable<Agent, AgentExtendedData>();

    public static bool IsLocallyControlled(this Agent agent)
    {
        // Prefer the installed authority seam (a coop battle installs it): this keeps the per-blow hot path off
        // the container resolve below. Non-battle missions (board games, taverns) never install the bridge, so
        // fall back to resolving the per-mission registry directly.
        if (BattleSpawnGate.AgentAuthority is IAgentAuthority authority)
            return authority.IsMine(agent);

        if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var networkAgentRegistry))
            return false;

        return networkAgentRegistry.IsLocallyControlled(agent);
    }

    public static Vec2 GetLastInputVector(this Agent agent)
    {
        return agentAdditions.GetOrCreateValue(agent).LastInputVector;
    }

    public static void SetLastInputVector(this Agent agent, Vec2 lastInputVector) 
    { 
        agentAdditions.GetOrCreateValue(agent).LastInputVector = lastInputVector;
    }

    public static bool InputVectorChanged(this Agent agent)
    {
        Vec2 inputVector = agent.MovementInputVector;

        var x1 = MathF.Round(inputVector.X, 2);
        var y1 = MathF.Round(inputVector.Y, 2);

        var lastInputVector = agent.GetLastInputVector();

        var x2 = MathF.Round(lastInputVector.X, 2);
        var y2 = MathF.Round(lastInputVector.Y, 2);

        return x1 != x2 || y1 != y2;
    }
}

internal class AgentExtendedData
{
    public Vec2 LastInputVector { get; set; }
}
