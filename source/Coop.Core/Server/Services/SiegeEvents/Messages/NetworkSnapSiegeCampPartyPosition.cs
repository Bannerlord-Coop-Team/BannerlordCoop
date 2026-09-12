using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace Coop.Core.Server.Services.SiegeEvents.Messages;

/// <summary>
/// Server rolled a besieging party's camp position; every client snaps its copy of the party to it.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkSnapSiegeCampPartyPosition : IEvent
{
    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public CampaignVec2 Position { get; }

    public NetworkSnapSiegeCampPartyPosition(string partyId, CampaignVec2 position)
    {
        PartyId = partyId;
        Position = position;
    }
}
