using Common;
using Common.Messaging;
using GameInterface.Services.Alleys.Interfaces;
using GameInterface.Services.Alleys.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Alleys.Patches;

/// <summary>
/// Routes the owning client's alley management actions (abandon, change overseer, manage garrison)
/// to the server instead of mutating locally, since alley ownership and party rosters are
/// server-authoritative. These methods only run on the client (the behavior is off on the host);
/// each patch publishes a local request event that <c>AlleyManagementHandler</c> forwards to the
/// server, then suppresses the vanilla local mutation (whose SetOwner / TeleportHeroAction would be
/// blocked on the client anyway).
/// </summary>
[HarmonyPatch(typeof(AlleyCampaignBehavior))]
internal class AlleyManagementPatches
{
    [HarmonyPatch("abandon_alley_consequence")]
    [HarmonyPrefix]
    private static bool AbandonAlleyConsequencePrefix(MenuCallbackArgs args)
    {
        if (ModInformation.IsServer) return true;

        if (TryGetCurrentSettlementAlley(out var alley))
        {
            MessageBroker.Instance.Publish(alley, new AbandonAlleyRequested(alley, fromClanScreen: false));
        }

        GameMenu.SwitchToMenu("town_outside");
        return false;
    }

    [HarmonyPatch("abandon_alley_from_dialog_consequence")]
    [HarmonyPrefix]
    private static bool AbandonAlleyFromDialogConsequencePrefix()
    {
        if (ModInformation.IsServer) return true;

        if (TryGetCurrentSettlementAlley(out var alley))
        {
            MessageBroker.Instance.Publish(alley, new AbandonAlleyRequested(alley, fromClanScreen: false));
        }

        return false;
    }

    [HarmonyPatch(nameof(AlleyCampaignBehavior.AbandonAlleyFromClanMenu))]
    [HarmonyPrefix]
    private static bool AbandonAlleyFromClanMenuPrefix(Alley alley)
    {
        if (ModInformation.IsServer) return true;

        MessageBroker.Instance.Publish(alley, new AbandonAlleyRequested(alley, fromClanScreen: true));
        return false;
    }

    [HarmonyPatch(nameof(AlleyCampaignBehavior.ChangeAlleyMember))]
    [HarmonyPrefix]
    private static bool ChangeAlleyMemberPrefix(Alley alley, Hero newAlleyLead)
    {
        if (ModInformation.IsServer) return true;

        MessageBroker.Instance.Publish(alley, new ChangeAlleyOverseerRequested(alley, newAlleyLead));
        return false;
    }

    [HarmonyPatch("ChangeAssignedClanMemberOfAlley")]
    [HarmonyPrefix]
    private static bool ChangeAssignedClanMemberOfAlleyPrefix(List<InquiryElement> newClanMemberInquiryElement)
    {
        if (ModInformation.IsServer) return true;

        if (newClanMemberInquiryElement == null || newClanMemberInquiryElement.Count == 0) return false;
        if (!TryGetCurrentSettlementAlley(out var alley)) return false;

        var newOverseer = (newClanMemberInquiryElement.First().Identifier as CharacterObject)?.HeroObject;
        if (newOverseer == null) return false;

        MessageBroker.Instance.Publish(alley, new ChangeAlleyOverseerRequested(alley, newOverseer));
        return false;
    }

    [HarmonyPatch("OnPartyScreenClosed")]
    [HarmonyPostfix]
    private static void OnPartyScreenClosedPostfix(TroopRoster leftMemberRoster)
    {
        if (ModInformation.IsServer) return;

        if (TryGetCurrentSettlementAlley(out var alley))
        {
            MessageBroker.Instance.Publish(alley, new SetAlleyGarrisonRequested(alley, leftMemberRoster));
        }
    }

    private static bool TryGetCurrentSettlementAlley(out Alley alley)
    {
        alley = null;
        if (!ContainerProvider.TryResolve<IAlleyCampaignBehaviorInterface>(out var behaviorInterface)) return false;
        return behaviorInterface.TryGetCurrentSettlementAlley(out alley);
    }
}
