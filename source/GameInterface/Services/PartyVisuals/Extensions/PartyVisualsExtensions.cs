using Common.Extensions;
using SandBox.View.Map;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Extensions;

public static class PartyVisualsExtensions
{
    public static void CreateNewPartyVisual(this PartyBase partyBase)
    {
        if (partyBase == null) return;

        if (Campaign.Current == null) return;
        if (PartyVisualManager.Current == null) return;

        PartyVisualManager.Current.AddNewPartyVisualForParty(partyBase);
    }
}
