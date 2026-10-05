using Common.Messaging;

namespace Missions.Messages;

/// <summary>[Game thread] Published by the battle controller as this client leaves, while the mission mesh still sends.</summary>
public record BattleMissionLeaving(string InstanceId) : IEvent;
