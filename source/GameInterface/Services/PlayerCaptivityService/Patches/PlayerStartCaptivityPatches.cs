using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using HarmonyLib;
using Serilog;
using System;
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

    // Capture the previous captor before the setter overwrites it. In the postfix the auto-property already
    // holds the new value, so the "did not change" check must compare against this snapshot, not the getter.
    [HarmonyPatch(typeof(Hero), nameof(Hero.PartyBelongedToAsPrisoner), MethodType.Setter)]
    [HarmonyPrefix]
    private static void Prefix_PartyBelongedToAsPrisoner(Hero __instance, out PartyBase __state)
    {
        __state = __instance.PartyBelongedToAsPrisoner;
    }

    [HarmonyPatch(typeof(Hero), nameof(Hero.PartyBelongedToAsPrisoner), MethodType.Setter)]
    [HarmonyPostfix]
    private static void Postfix_PartyBelongedToAsPrisoner(Hero __instance, PartyBase value, PartyBase __state)
    {
        if (ModInformation.IsServer) return;

        // Did not change
        if (__state == value) return;

        // Skip if not main hero
        if (!__instance.IsHeroControlled()) return;

        var message = new PlayerCaptivityChanged(value);
        MessageBroker.Instance.Publish(null, message);
    }

    // Runs BEFORE native CaptureDefeatedPartyMembers. Native removes the defeated party's leader
    // (RemovePartyLeader) and probabilistically scatters heroes as fugitives, which would defeat a postfix
    // safety net. Running first, TakePrisonerAction.Apply clears the player party's roster on the server, so
    // native's loop no longer re-processes the captured hero.
    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.CaptureDefeatedPartyMembers))]
    [HarmonyPrefix]
    private static void Prefix_CaptureDefeatedPartyMembers(MapEvent __instance, MBReadOnlyList<MapEventParty> winnerParties, MBReadOnlyList<MapEventParty> defeatedParties)
    {
        if (__instance.RetreatingSide != BattleSideEnum.None)
            return;

        foreach (var party in defeatedParties)
        {
            var defeatedParty = party.Party?.MobileParty;

            // Skip AI parties
            if (defeatedParty?.IsPlayer() != true)
                continue;

            // A player party's leader (PartyComponent.Leader) is not reliably set on the server, so resolve the
            // captured hero from the authoritative player registry rather than party.LeaderHero.
            if (!TryGetRegisteredPlayerHero(defeatedParty, out var playerHero))
            {
                Logger.Error("Defeated player party {PartyId} has no registered hero, skipping prisoner capture", defeatedParty.StringId);
                continue;
            }

            PartyBase captorParty = winnerParties.WhereQ((MapEventParty x) => x.Party.MemberRoster.TotalManCount > 0).MaxBy((MapEventParty x) => x.ContributionToBattle).Party;
            if (captorParty.IsMobile && (captorParty.MobileParty.IsMilitia || captorParty.MobileParty.IsGarrison))
            {
                captorParty = captorParty.MobileParty.HomeSettlement.Party;
            }
            TakePrisonerAction.Apply(captorParty, playerHero);
        }
    }

    private static bool TryGetRegisteredPlayerHero(MobileParty party, out Hero hero)
    {
        hero = null;

        if (!PlayerRegistry.PlayerObjects.TryGetValue(party, out var player))
            return false;

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return false;

        return objectManager.TryGetObject(player.HeroId, out hero);
    }
}
