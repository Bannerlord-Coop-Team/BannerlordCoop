using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>Local notification of an agent's actual formation membership changing in a co-op battle.</summary>
public record BattleAgentFormationChanged(Agent Agent, int FormationIndex) : IEvent;
