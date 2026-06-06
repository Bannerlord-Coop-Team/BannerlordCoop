using SandBox.View.Map.Managers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Extensions;

public static class PartyVisualsExtensions
{
    public static void CreateNewPartyVisual(this MobileParty partyBase)
    {
        if (partyBase == null) return;

        if (Campaign.Current == null) return;
        if (MobilePartyVisualManager.Current == null) return;

        MobilePartyVisualManager.Current.AddNewPartyVisualForParty(partyBase);
    }
}
