using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.PlayerCaptivityService.Messages;

/// <summary>
/// Server → clients: the authoritative map position of a player party just freed from captivity.
/// <see cref="TaleWorlds.CampaignSystem.Party.MobileParty"/>.Position is not auto-synced, so the
/// reactivated party would otherwise sit at its stale pre-capture spot on other clients.
/// </summary>
[ProtoContract]
internal readonly struct NetworkFreedPrisonerPosition : IEvent
{
    [ProtoMember(1)]
    public readonly string PartyId;
    [ProtoMember(2)]
    public readonly CampaignVec2 Position;
    [ProtoMember(3)]
    public readonly bool IsCurrentlyAtSea;

    public NetworkFreedPrisonerPosition(string partyId, CampaignVec2 position, bool isCurrentlyAtSea)
    {
        PartyId = partyId;
        Position = position;
        IsCurrentlyAtSea = isCurrentlyAtSea;
    }
}
