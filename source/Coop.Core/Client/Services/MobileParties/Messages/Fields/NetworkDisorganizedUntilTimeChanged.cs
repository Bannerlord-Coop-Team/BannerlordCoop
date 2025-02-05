using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Command to change the field _disorganizedUntilTime of a mobile party.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkDisorganizedUntilTimeChanged : ICommand
{
    [ProtoMember(1)]
    public long DisorganizedUntilTime { get; }
    [ProtoMember(2)]
    public string MobilePartyId { get; }

    public NetworkDisorganizedUntilTimeChanged(long disorganizedUntilTime, string mobilePartyId)
    {
        DisorganizedUntilTime = disorganizedUntilTime;
        MobilePartyId = mobilePartyId;
    }
}