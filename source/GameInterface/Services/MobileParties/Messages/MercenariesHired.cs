using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// [Client local] A player hired mercenaries through a town's recruitment dialogue. The conversing
/// client suppresses the local apply and publishes this so the server adds the troops and deducts
/// the gold authoritatively, replicating both to every peer.
/// </summary>
internal readonly struct MercenariesHired : IEvent
{
    public readonly Hero MainHero;
    public readonly MobileParty MainParty;
    public readonly Town Town;
    public readonly CharacterObject MercenaryTroop;
    public readonly int Count;
    public readonly int GoldAmount;

    public MercenariesHired(
        Hero mainHero,
        MobileParty mainParty,
        Town town,
        CharacterObject mercenaryTroop,
        int count,
        int goldAmount)
    {
        MainHero = mainHero;
        MainParty = mainParty;
        Town = town;
        MercenaryTroop = mercenaryTroop;
        Count = count;
        GoldAmount = goldAmount;
    }
}
