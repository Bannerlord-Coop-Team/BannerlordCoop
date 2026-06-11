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
    private static void Prefix_ApplyInternal(PartyBase capturerParty, Hero prisonerCharacter)
    {
        // Re-entrant call below, or a server-approved original: run it.
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client called managed method {methodName}", $"{nameof(TakePrisonerAction)}.{nameof(TakePrisonerAction.ApplyInternal)}");
            return;
        }

        var prisonerParty = prisonerCharacter.PartyBelongedTo;
        if (prisonerParty?.IsPlayerParty() != true)
            return true;

        using (new AllowedThread())
        {
            TakePrisonerAction.Apply(capturerParty, prisonerCharacter);
        }

        MessageBroker.Instance.Publish(null, new PrisonerTaken(capturerParty, prisonerCharacter, prisonerParty));

        return false;
    }
}
