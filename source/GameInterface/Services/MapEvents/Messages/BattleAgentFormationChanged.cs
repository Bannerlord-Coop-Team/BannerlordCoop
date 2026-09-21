using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Messages;

public record BattleAgentFormationChanged(Agent Agent) : IEvent;
