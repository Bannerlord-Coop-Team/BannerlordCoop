using Common.Extensions;
using SandBox.View.Map;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyBases.Extensions;

public static class PartyBaseExtensions
{
    static Func<PartyVisualManager, Dictionary<PartyBase, PartyVisual>> PartyVisualsManager_partiesAndVisuals = typeof(PartyVisualManager)
        .GetField("_partiesAndVisuals", BindingFlags.NonPublic | BindingFlags.Instance)
        .BuildUntypedGetter<PartyVisualManager, Dictionary<PartyBase, PartyVisual>>();

    public static PartyVisual GetPartyVisual(this PartyBase partyBase)
    {
        if (partyBase == null) return null;

        if (PartyVisualManager.Current == null) return null;

        return PartyVisualsManager_partiesAndVisuals(PartyVisualManager.Current).TryGetValue(partyBase, out var partyVisual) ? partyVisual : null;
    }
}
