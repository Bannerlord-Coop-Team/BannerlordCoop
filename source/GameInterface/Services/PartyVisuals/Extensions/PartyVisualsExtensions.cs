using Common.Extensions;
using SandBox.View.Map;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Extensions;

public static class PartyVisualsExtensions
{
    static Action<PartyVisualManager, PartyBase> PartyVisualsManager_AddNewPartyVisualForParty = typeof(PartyVisualManager)
        .GetMethod("AddNewPartyVisualForParty", BindingFlags.NonPublic | BindingFlags.Instance)
        .BuildDelegate<Action<PartyVisualManager, PartyBase>>();

    public static void CreateNewPartyVisual(this PartyBase partyBase)
    {
        if (partyBase == null) return;

        if (Campaign.Current == null) return;
        if (PartyVisualManager.Current == null) return;

        PartyVisualsManager_AddNewPartyVisualForParty(PartyVisualManager.Current, partyBase);
    }
}
