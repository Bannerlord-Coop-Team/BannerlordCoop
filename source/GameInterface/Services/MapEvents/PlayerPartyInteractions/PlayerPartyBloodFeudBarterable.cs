using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Localization;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

/// <summary>
/// Ends a blood feud between two player clans once both clan leaders offer it in a trade
/// </summary>
internal class PlayerPartyBloodFeudBarterable : Barterable
{
    public const string Id = "end_blood_feud_barterable";

    private readonly Hero otherHero;
    private readonly PartyBase otherParty;

    public override int MaxAmount => 1;
    public override TextObject Name => GameTexts.FindText("str_coop_player_party_blood_feud_barterable");
    public override string StringID => Id;

    public PlayerPartyBloodFeudBarterable(
        Hero ownerHero,
        Hero otherHero,
        PartyBase ownerParty,
        PartyBase otherParty) : base(ownerHero, ownerParty)
    {
        this.otherHero = otherHero;
        this.otherParty = otherParty;
    }

    public override void Apply()
    {
    }

    public override int GetUnitValueForFaction(IFaction faction)
    {
        if (faction == otherHero?.MapFaction || faction == otherParty?.MapFaction)
            return 1;

        return -1;
    }

    public override ImageIdentifier GetVisualIdentifier()
        => null;

    // Ending a feud is each clan's decision, so both players must lead the clans in it
    public static bool TryGetLeaderClans(PartyBase ownerParty, PartyBase otherParty, out Clan ownerClan, out Clan otherClan)
    {
        Hero ownerHero = ownerParty?.LeaderHero;
        Hero otherHero = otherParty?.LeaderHero;
        ownerClan = ownerHero?.Clan;
        otherClan = otherHero?.Clan;

        if (ownerClan == null || otherClan == null || ownerClan == otherClan) return false;

        return ownerHero.IsClanLeader && otherHero.IsClanLeader;
    }
}
