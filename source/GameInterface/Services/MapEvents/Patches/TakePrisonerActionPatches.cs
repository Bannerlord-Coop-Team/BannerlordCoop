using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(TakePrisonerAction))]
internal class TakePrisonerActionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<TakePrisonerActionPatches>();

    [HarmonyPatch(nameof(TakePrisonerAction.Apply))]
    [HarmonyPrefix]
    private static bool PrefixApply(PartyBase capturerParty, Hero prisonerCharacter)
    {
        // Re-entrant call below, or a server-approved original: run it.
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client called managed method {methodName}", $"{nameof(TakePrisonerAction)}.{nameof(TakePrisonerAction.Apply)}");
            return true;
        }

        // Only player-party captures are replicated through PrisonerTaken; everything else keeps vanilla
        // behaviour (its roster moves sync normally through the TroopRoster patches).
        var prisonerParty = prisonerCharacter.PartyBelongedTo;
        if (prisonerParty?.IsPlayerParty() != true)
            return true;

        // Apply the capture once under AllowedThread so the member/prison roster moves do NOT also broadcast
        // through the TroopRoster patches. Otherwise the client applies the hero removal twice - once from the
        // roster sync and once from NetworkTakePrisoner -> ApplyInternal - which corrupts its roster (wrong
        // troop count, missing hero). The capture is replicated authoritatively via PrisonerTaken instead.
        // Apply clears the hero's PartyBelongedTo, so the prisoner's party is captured above and carried on
        // the message for handlers that need it.
        using (new AllowedThread())
        {
            TakePrisonerAction.Apply(capturerParty, prisonerCharacter);
        }

        MessageBroker.Instance.Publish(null, new PrisonerTaken(capturerParty, prisonerCharacter, prisonerParty));

        return false;
    }
}
