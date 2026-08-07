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
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.PlayerCaptivityService.Patches;

/// <summary>
/// Server-authoritative start of player captivity. The server decides a defeated player hero is
/// captured (<see cref="MapEvent.CaptureDefeatedPartyMembers"/>) and applies
/// <see cref="TakePrisonerAction"/> with patches live, so each side effect replicates as its own
/// message (roster deltas + the auto-synced <see cref="Hero.PartyBelongedToAsPrisoner"/>). Clients
/// only react to the replicated state.
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
        // locally; it receives every capture as the server's replicated side effects (roster deltas +
        // auto-synced captivity state).
        if (ModInformation.IsClient) return false;

        if (__instance.RetreatingSide != BattleSideEnum.None)
            return true;

        CaptureDefeatedPlayerHeroes(winnerParties, defeatedParties);

        return true;
    }

    /// <summary>
    /// Takes every defeated player hero prisoner, resolving the hero from the player registry. Must run
    /// BEFORE the AI capture loop — the native one (via <see cref="Prefix_CaptureDefeatedPartyMembers"/>) or
    /// the coop reimplementation (<c>MapEventResultsInterface.CaptureDefeatedPartyMembers</c>), which skips
    /// player heroes and relies on this having already run — because <see cref="TakePrisonerAction.Apply"/>
    /// clears the player party's roster on the server (PlayerCaptivityServerHandler), so the loop can no
    /// longer re-process or scatter the captured hero's men.
    /// </summary>
    internal static void CaptureDefeatedPlayerHeroes(MBReadOnlyList<MapEventParty> winnerParties, MBReadOnlyList<MapEventParty> defeatedParties)
    {
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

            // The capture can be reached more than once for the same battle (e.g. a surrender applies the
            // victory immediately and the client's battle-state relay then re-applies the same state). Skip
            // a hero already taken prisoner on the first pass to avoid a duplicate capture.
            if (playerHero.IsPrisoner)
                continue;

            if (!TryResolveCaptorParty(winnerParties, out var captorParty))
            {
                Logger.Warning("Could not resolve a captor party for defeated player party {PartyId}, skipping prisoner capture", defeatedParty.StringId);
                continue;
            }

            if (captorParty.IsMobile && (captorParty.MobileParty.IsMilitia || captorParty.MobileParty.IsGarrison))
            {
                captorParty = captorParty.MobileParty.HomeSettlement.Party;
            }
            TakePrisonerAction.Apply(captorParty, playerHero);

            ScheduleBesiegerCampClear(defeatedParty);
        }
    }

    /// <summary>
    /// Removes the captured player's party from its besieger camp — the defeat side effect native runs in
    /// <c>PlayerEncounter.DoPlayerDefeat</c>, which a server-committed defeat never reaches (the defeated
    /// client's encounter is staged straight to <c>PlayerEncounterState.End</c>), and which neither
    /// <see cref="TakePrisonerAction"/> nor the coop park performs — so the prisoner's party kept besieging
    /// on the server. The write runs with patches live, so the cleared auto-synced
    /// <see cref="MobileParty.BesiegerCamp"/> replicates to every client.
    /// <para>
    /// Deferred to the next game-thread drain rather than cleared inline: the capture runs inside the battle
    /// result commit, and when the captured party is the LAST besieger the clear cascades into
    /// <c>SiegeEvent.FinalizeSiegeEvent</c>, which re-finalizes the besieged settlement's live map event —
    /// the very event being committed when the defeat is a wall assault. Native sequences this clear outside
    /// the commit too: the player's runs in <c>DoPlayerDefeat</c> after <c>Finish()</c> tore the event down,
    /// and a defeated AI party's runs in <c>MobileParty.RemoveParty</c> on a later tick.
    /// </para>
    /// </summary>
    private static void ScheduleBesiegerCampClear(MobileParty defeatedParty)
    {
        if (defeatedParty.BesiegerCamp == null) return;

        Logger.Debug("Scheduling besieger camp clear for captured player party {PartyId}", defeatedParty.StringId);

        GameThread.EnqueueSafe(() =>
        {
            // The camp can already be gone when the queue drains (the defeated client's native defeat
            // clear may have reached the server first, or the siege was torn down with the battle); the
            // clear must not resurrect the cascade then.
            if (defeatedParty.BesiegerCamp == null) return;

            defeatedParty.BesiegerCamp = null;
        }, context: nameof(ScheduleBesiegerCampClear));
    }

    private static bool TryResolveCaptorParty(MBReadOnlyList<MapEventParty> winnerParties, out PartyBase captorParty)
    {
        MapEventParty captor = null;
        captorParty = null;

        foreach (var party in winnerParties)
        {
            var partyBase = party?.Party;
            if (partyBase?.MemberRoster == null || partyBase.MemberRoster.TotalManCount <= 0)
                continue;

            if (captor == null || party.ContributionToBattle > captor.ContributionToBattle)
                captor = party;
        }

        // No winner has men left (e.g. a raid repelled by a village whose settlement party holds no
        // troops). Native's chance-based capture has no man-count requirement, so fall back to the
        // highest-contribution winner rather than leaving the defeated player uncaptured.
        if (captor == null)
        {
            foreach (var party in winnerParties)
            {
                if (party?.Party == null)
                    continue;

                if (captor == null || party.ContributionToBattle > captor.ContributionToBattle)
                    captor = party;
            }
        }

        if (captor == null)
            return false;

        captorParty = captor.Party;
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
