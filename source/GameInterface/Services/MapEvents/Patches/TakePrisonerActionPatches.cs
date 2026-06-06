using Common;
using Common.Logging;
using Common.Messaging;
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
    private static void PrefixApply(PartyBase capturerParty, Hero prisonerCharacter)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client called managed method {methodName}", $"{nameof(TakePrisonerAction)}.{nameof(TakePrisonerAction.Apply)}");
            return;
        }

        if (prisonerCharacter.PartyBelongedTo?.IsPlayerParty() == false)
            return;

        if (prisonerCharacter.PartyBelongedTo == null)
            return;

        var message = new PrisonerTaken(capturerParty, prisonerCharacter);
        MessageBroker.Instance.Publish(null, message);
    }
}
