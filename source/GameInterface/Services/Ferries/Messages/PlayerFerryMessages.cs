using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Ferries.Messages;

/// <summary>
/// [Client, local] The player chose Continue on arriving with the ferry.
/// </summary>
public readonly struct PlayerFerryEndAttempted : IEvent
{
    public readonly MobileParty PlayerParty;

    public PlayerFerryEndAttempted(MobileParty playerParty)
    {
        PlayerParty = playerParty;
    }
}

/// <summary>
/// [Client to server] Lands the player's party at its ferry's target village.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestEndFerry : ICommand
{
    [ProtoMember(1)]
    public readonly string PartyId;

    public NetworkRequestEndFerry(string partyId)
    {
        PartyId = partyId;
    }
}

/// <summary>
/// [Server, local] A player got on a ferry at the village, or off it when <see cref="VillageId"/> is null.
/// </summary>
public readonly struct PlayerFerryChanged : IEvent
{
    public readonly string PlayerHeroId;
    public readonly string VillageId;

    public PlayerFerryChanged(string playerHeroId, string villageId)
    {
        PlayerHeroId = playerHeroId;
        VillageId = villageId;
    }
}

/// <summary>
/// [Server to clients] A player got on a ferry at the village, or off it when <see cref="VillageId"/> is null.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerFerryChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;
    [ProtoMember(2)]
    public readonly string VillageId;

    public NetworkPlayerFerryChanged(string playerHeroId, string villageId)
    {
        PlayerHeroId = playerHeroId;
        VillageId = villageId;
    }
}