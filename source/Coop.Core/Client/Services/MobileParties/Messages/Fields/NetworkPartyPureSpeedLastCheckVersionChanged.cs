using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Command to change the property _partyPureSpeedLastCheckVersion of a mobile party.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkPartyPureSpeedLastCheckVersionChanged : ICommand
{
    [ProtoMember(1)]
    public int PartyPureSpeedLastCheckVersion { get; }
    [ProtoMember(2)]
    public string MobilePartyId { get; }

    public NetworkPartyPureSpeedLastCheckVersionChanged(int partyPureSpeedLastCheckVersion, string mobilePartyId)
    {
        PartyPureSpeedLastCheckVersion = partyPureSpeedLastCheckVersion;
        MobilePartyId = mobilePartyId;
    }
}