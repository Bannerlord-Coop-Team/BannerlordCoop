using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Localization;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

internal class PlayerPartyTroopBarterable : Barterable
{
    private readonly Hero otherHero;
    private readonly PartyBase otherParty;
    private readonly TroopRosterElement troopRosterElement;

    public TroopRosterElement TroopRosterElement => troopRosterElement;

    public override int MaxAmount => troopRosterElement.Number;
    public override TextObject Name => troopRosterElement.Character?.Name ?? new TextObject("{=MpPSKj5s}Troop");
    // BarterItemVisualBrushWidget only enables ImageIdentifierWidget for known native barterable type ids.
    public override string StringID => "item_barterable";

    public PlayerPartyTroopBarterable(
        Hero ownerHero,
        Hero otherHero,
        PartyBase ownerParty,
        PartyBase otherParty,
        TroopRosterElement troopRosterElement) : base(ownerHero, ownerParty)
    {
        this.otherHero = otherHero;
        this.otherParty = otherParty;
        this.troopRosterElement = troopRosterElement;
    }

    public override void Apply()
    {
    }

    public override int GetUnitValueForFaction(IFaction faction)
    {
        if (faction == otherHero?.MapFaction || faction == otherParty?.MapFaction)
            return Math.Max(1, troopRosterElement.Character?.Level ?? 1);

        return -Math.Max(1, troopRosterElement.Character?.Level ?? 1);
    }

    public override ImageIdentifier GetVisualIdentifier()
    {
        if (troopRosterElement.Character == null) return null;

        return new CharacterImageIdentifier(CharacterCode.CreateFrom(troopRosterElement.Character));
    }
}
