using TaleWorlds.MountAndBlade;
using Missions.Services.Network;
using System.Runtime.CompilerServices;
using TaleWorlds.Library;
using static TaleWorlds.MountAndBlade.Agent;

namespace Missions.Services.Agents.Extensions
{
    public static class AgentExtensions
    {
        private static readonly ConditionalWeakTable<Agent, AgentAdditions> agentAdditions = new ConditionalWeakTable<Agent, AgentAdditions>();

        public static bool IsNetworkAgent(this Agent agent)
        {
            return NetworkAgentRegistry.Instance.IsAgentRegistered(agent);
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

    internal class AgentAdditions
    {
        public Vec2 LastInputVector { get; set; }
    }
}
