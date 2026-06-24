using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// [Client -> Server] Requests the server apply a tavern mercenary hire: add the mercenary troops
/// to the player's party member roster and deduct the gold cost. The server applies both with
/// patches live, so the troop add (TroopRoster patches) and gold change (Hero.Gold sync) replicate
/// to every client. The count is the client's requested hire amount; the server validates it against
/// authoritative stock and applies the client-computed price against the client gold snapshot.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct HireMercenaries : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;
    [ProtoMember(2)]
    public readonly string MainPartyId;
    [ProtoMember(3)]
    public readonly string TownId;
    [ProtoMember(4)]
    public readonly string MercenaryTroopId;
    [ProtoMember(5)]
    public readonly int Count;
    [ProtoMember(6)]
    public readonly int GoldAmount;
    [ProtoMember(7)]
    public readonly int HeroGold;

    public HireMercenaries(
        string mainHeroId,
        string mainPartyId,
        string townId,
        string mercenaryTroopId,
        int count,
        int goldAmount,
        int heroGold)
    {
        MainHeroId = mainHeroId;
        MainPartyId = mainPartyId;
        TownId = townId;
        MercenaryTroopId = mercenaryTroopId;
        Count = count;
        GoldAmount = goldAmount;
        HeroGold = heroGold;
    }
}
