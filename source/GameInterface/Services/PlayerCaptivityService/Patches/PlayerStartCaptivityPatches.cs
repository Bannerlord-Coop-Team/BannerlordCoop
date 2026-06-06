using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.PlayerCaptivityService.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;

namespace GameInterface.Services.PlayerCaptivityService.Patches;

[HarmonyPatch]
internal class PlayerStartCaptivityPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerStartCaptivityPatches>();

    [HarmonyPatch(typeof(Hero), nameof(Hero.PartyBelongedToAsPrisoner), MethodType.Setter)]
    [HarmonyPostfix]
    private static void Postfix_PartyBelongedToAsPrisoner(Hero __instance, PartyBase value)
    {
        if (ModInformation.IsServer) return;

        // Did not change
        if (__instance.PartyBelongedToAsPrisoner == value) return;

        // Skip if not main hero
        if (__instance != Hero.MainHero) return;

        var message = new PlayerCaptivityChanged(value);
        MessageBroker.Instance.Publish(null, message);
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.CaptureDefeatedPartyMembers))]
    [HarmonyPostfix]
    private static void Postfix_CaptureDefeatedPartyMembers(MapEvent __instance, MBReadOnlyList<MapEventParty> winnerParties, MBReadOnlyList<MapEventParty> defeatedParties)
    {
        if (__instance.RetreatingSide != BattleSideEnum.None)
            return;

        foreach (var party in defeatedParties)
        {
            // Skip AI parties
            if (party.Party?.MobileParty?.IsPlayerParty() != true)
                continue;

            if (party.Party.LeaderHero == null)
            {
                Logger.Error("Defeated party {PartyId} has no leader hero, skipping prisoner capture", party.Party.MobileParty.StringId);
                continue;
            }

            PartyBase captorParty = winnerParties.WhereQ((MapEventParty x) => x.Party.MemberRoster.TotalManCount > 0).MaxBy((MapEventParty x) => x.ContributionToBattle).Party;
            if (captorParty.IsMobile && (captorParty.MobileParty.IsMilitia || captorParty.MobileParty.IsGarrison))
            {
                captorParty = captorParty.MobileParty.HomeSettlement.Party;
            }
            TakePrisonerAction.Apply(captorParty, party.Party.LeaderHero);
        }
    }
}
