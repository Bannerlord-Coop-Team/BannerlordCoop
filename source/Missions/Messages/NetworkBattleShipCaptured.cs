using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// [Client to server] The sender captured the hull standing for <see cref="CampaignShipId"/> in the battle's mission, so the
/// server takes that ship's fates from it alone. Sent on the channel its fates use, so it arrives before any of them.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkBattleShipCaptured : ICommand
{
    [ProtoMember(1)] public readonly string InstanceId;
    [ProtoMember(2)] public readonly string CampaignShipId;
    /// <summary>The hull's place in its capture order after this capture; the server keeps the first captor per place.</summary>
    [ProtoMember(3)] public readonly int CaptureSequence;

    public NetworkBattleShipCaptured(string instanceId, string campaignShipId, int captureSequence)
    {
        InstanceId = instanceId;
        CampaignShipId = campaignShipId;
        CaptureSequence = captureSequence;
    }
}
