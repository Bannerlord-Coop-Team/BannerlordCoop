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

    [HarmonyPatch(nameof(TakePrisonerAction.ApplyInternal))]
    [HarmonyPrefix]
    private static bool Prefix_ApplyInternal(PartyBase capturerParty, Hero prisonerCharacter)
    {
        // Re-entrant call below, or a server-approved original: run it.
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client called managed method {methodName}", $"{nameof(TakePrisonerAction)}.{nameof(TakePrisonerAction.ApplyInternal)}");
            return false;
        }

        var prisonerParty = prisonerCharacter.PartyBelongedTo;
        if (prisonerParty?.IsPlayerParty() != true)
            return true;

        // The native capture runs silenced: none of its side effects (member-roster removal, prison-roster
        // add, hero state) replicate from here. Every client derives them instead by replaying this same
        // action when it applies NetworkTakePrisoner (MapEventPartyHandler.Handle_NetworkTakePrisoner),
        // which also parks its copy of the captured party. A side effect added inside ApplyInternal that
        // the client replay does NOT derive identically will silently diverge per machine.
        using (new AllowedThread())
        {
            TakePrisonerAction.Apply(capturerParty, prisonerCharacter);
        }

        MessageBroker.Instance.Publish(null, new PrisonerTaken(capturerParty, prisonerCharacter, prisonerParty));

        return false;
    }
}
