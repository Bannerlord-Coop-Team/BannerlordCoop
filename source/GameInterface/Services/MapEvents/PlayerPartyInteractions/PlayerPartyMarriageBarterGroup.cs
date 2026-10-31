using TaleWorlds.CampaignSystem.BarterSystem;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

// Gives player trade marriages their own Marriages column instead of filling Misc.
internal class PlayerPartyMarriageBarterGroup : BarterGroup
{
    public override float AIDecisionWeight => 0.25f;
}
