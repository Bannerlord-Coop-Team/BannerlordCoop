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
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;

namespace GameInterface.Services.PlayerCaptivityService.Patches;

/// <summary>
/// Server-authoritative start of player captivity. The server decides a defeated player hero is
/// captured (<see cref="MapEvent.CaptureDefeatedPartyMembers"/>) and replicates it through
/// <see cref="TakePrisonerAction"/> (NetworkTakePrisoner + the synced
/// <see cref="Hero.PartyBelongedToAsPrisoner"/>). Clients only react to the replicated state.
/// </summary>
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

    // Client: when the synced captor of the hero this client controls changes, drive the local
    // captivity UI (PlayerCaptivityClientHandler switches to/away from the prisoner menus).
    [HarmonyPatch(typeof(Hero), nameof(Hero.PartyBelongedToAsPrisoner), MethodType.Setter)]
    [HarmonyPostfix]
    private static void Postfix_PartyBelongedToAsPrisoner(Hero __instance, PartyBase value, PartyBase __state)
    {
        if (ModInformation.IsServer) return;

        // Did not change
        if (__state == value) return;

        // Skip if not main hero
        if (!__instance.IsControlledByThisInstance()) return;

        var message = new PlayerCaptivityChanged(value);
        MessageBroker.Instance.Publish(null, message);
    }

    // Runs BEFORE native CaptureDefeatedPartyMembers. Native removes the defeated party's leader
    // (RemovePartyLeader) and probabilistically scatters heroes as fugitives, which would defeat a postfix
    // safety net. Running first, TakePrisonerAction.Apply clears the player party's roster on the server
    // (PlayerCaptivityServerHandler), so native's loop no longer re-processes the captured hero.
    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.CaptureDefeatedPartyMembers))]
    [HarmonyPrefix]
    private static bool Prefix_CaptureDefeatedPartyMembers(MapEvent __instance, MBReadOnlyList<MapEventParty> winnerParties, MBReadOnlyList<MapEventParty> defeatedParties)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Captures are server-authoritative. A client must never run the native scatter/capture loop
        // locally; it receives every capture as a replicated TakePrisonerAction (NetworkTakePrisoner).
        if (ModInformation.IsClient) return false;

        if (__instance.RetreatingSide != BattleSideEnum.None)
            return true;

        foreach (var party in defeatedParties)
        {
            var defeatedParty = party.Party?.MobileParty;

            // Skip AI parties; native handles them
            if (defeatedParty?.IsPlayerParty() != true)
                continue;

            // A player party's leader (PartyComponent.Leader) is not reliably set on the server, so resolve
            // the captured hero from the authoritative player registry rather than party.LeaderHero.
            if (!TryResolvePlayerHero(defeatedParty, out var playerHero))
            {
                Logger.Error("Could not resolve a hero for defeated player party {PartyId}, skipping prisoner capture", defeatedParty.StringId);
                continue;
            }

            // OnBattleWon commits the results twice on the server (the coop OnBattleWon prefix, then native
            // OnBattleWon for a non-player map event), so this prefix can run twice for the same battle. Skip
            // a hero already taken prisoner on the first pass to avoid a duplicate capture.
            if (playerHero.IsPrisoner)
                continue;

            PartyBase captorParty = winnerParties.WhereQ((MapEventParty x) => x.Party.MemberRoster.TotalManCount > 0).MaxBy((MapEventParty x) => x.ContributionToBattle).Party;
            if (captorParty.IsMobile && (captorParty.MobileParty.IsMilitia || captorParty.MobileParty.IsGarrison))
            {
                captorParty = captorParty.MobileParty.HomeSettlement.Party;
            }
            TakePrisonerAction.Apply(captorParty, playerHero);
        }

        return true;
    }

    /// <summary>
    /// Resolves the hero of a defeated player party from the player registry, falling back to
    /// <see cref="MobileParty.LeaderHero"/> when the registry has no mapping.
    /// </summary>
    private static bool TryResolvePlayerHero(MobileParty playerParty, out Hero playerHero)
    {
        playerHero = null;

        if (ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) &&
            ContainerProvider.TryResolve<IObjectManager>(out var objectManager) &&
            objectManager.TryGetId(playerParty, out var partyId))
        {
            var player = playerManager.Players.FirstOrDefault(p => p.MobilePartyId == partyId);
            if (player != null && objectManager.TryGetObject(player.HeroId, out playerHero))
                return true;
        }

        playerHero = playerHero ?? playerParty.LeaderHero;
        return playerHero != null;
    }
}
