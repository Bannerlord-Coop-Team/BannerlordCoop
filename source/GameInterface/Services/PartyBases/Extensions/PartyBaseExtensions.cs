using Common.Extensions;
using SandBox.View.Map;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyBases.Extensions;

public static class PartyBaseExtensions
{
    public static MobilePartyVisual GetPartyVisual(this PartyBase partyBase)
    {
        if (partyBase == null) return null;

        if (MobilePartyVisualManager.Current == null) return null;

        return MobilePartyVisualManager.Current._partiesAndVisuals.TryGetValue(partyBase, out var partyVisual) ? partyVisual : null;
    }
}
