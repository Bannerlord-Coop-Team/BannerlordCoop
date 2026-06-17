using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
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
    private static bool Prefix_ApplyInternal(Hero prisonerCharacter, ref MobileParty __state)
    {
        // A server-approved original (e.g. applying a received message): run it without the coop extras.
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client called managed method {methodName}", $"{nameof(TakePrisonerAction)}.{nameof(TakePrisonerAction.ApplyInternal)}");
            return false;
        }

        // The capture runs with patches live, so each side effect of the native body replicates as its
        // own message: the member-roster removal and the captor's prison-roster add as roster deltas,
        // hero state and PartyBelongedToAsPrisoner through their auto-synced setters. Clients apply the
        // state the server computed instead of re-deriving it. A captured player party additionally
        // needs the coop park; snapshot it here — native clears hero.PartyBelongedTo — and let the
        // postfix publish PrisonerTaken once the capture has fully applied.
        var prisonerParty = prisonerCharacter.PartyBelongedTo;
        if (prisonerParty?.IsPlayerParty() == true)
        {
            __state = prisonerParty;
        }

        return true;
    }

    [HarmonyPatch(nameof(TakePrisonerAction.ApplyInternal))]
    [HarmonyPostfix]
    private static void Postfix_ApplyInternal(PartyBase capturerParty, Hero prisonerCharacter, MobileParty __state)
    {
        // Only set when the prefix intercepted a player-party capture on the server.
        if (__state == null) return;

        MessageBroker.Instance.Publish(null, new PrisonerTaken(capturerParty, prisonerCharacter, __state));
    }
}
