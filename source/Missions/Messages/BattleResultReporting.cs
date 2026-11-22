using Common.Messaging;

namespace Missions.Messages;

/// <summary>
/// [Client, local] Published right before this client reports its battle's resolved result to the server, so a battle type's
/// own report (the naval ship fates) goes out first on the same relay channel.
/// </summary>
public readonly struct BattleResultReporting : IEvent
{
    public readonly string InstanceId;

    public BattleResultReporting(string instanceId)
    {
        InstanceId = instanceId;
    }
}
