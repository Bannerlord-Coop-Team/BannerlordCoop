using Common.Extensions;
using SandBox.View.Map;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyBases.Extensions;

public static class PartyBaseExtensions
{
    public static PartyVisual GetPartyVisual(this PartyBase partyBase)
    {
        if (partyBase == null) return null;

        if (PartyVisualManager.Current == null) return null;

        return PartyVisualManager.Current._partiesAndVisuals.TryGetValue(partyBase, out var partyVisual) ? partyVisual : null;
    }
}
