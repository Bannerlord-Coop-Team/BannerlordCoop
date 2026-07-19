using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.PartyVisuals.Extensions;
using GameInterface.Services.PartyVisuals.Messages;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using Helpers;
using LiteNetLib;
using SandBox.View.Map.Managers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.PlayerCaptivityService.Handlers;

/// <summary>
/// Server side of player captivity. The server is authoritative for when a player hero is
/// captured and released:
/// <list type="bullet">
/// <item><see cref="PrisonerTaken"/> — a player hero is being captured after a lost battle; park the
/// player's party so native post-battle processing cannot scatter it.</item>
/// <item><see cref="NetworkPlayerSurrendered"/> — a client chose to surrender; resolve the battle on
/// the server, which then captures the player through the normal defeat path.</item>
/// <item><see cref="NetworkEndPlayerCaptivityAttempted"/> — a client requests release from captivity;
/// apply it and confirm with <see cref="NetworkPlayerCaptivityEnded"/>.</item>
/// <item><see cref="NetworkEndCaptivityAttempted"/> — a client chose to release another hero;
/// apply the release through the authoritative game action.</item>
/// <item><see cref="NetworkPrisonerLiberationAttempted"/> — a client liberated a prisoner through
/// the post-battle conversation; apply the vanilla relation reward for that client hero.</item>
/// <item><see cref="CampaignTick"/> — keep captive players' parties glued to their captor
/// (the server-side replacement for native <see cref="PlayerCaptivity"/>.Update).</item>
/// </list>
/// The client counterpart is <see cref="PlayerCaptivityClientHandler"/>.
/// </summary>
internal class PlayerCaptivityServerHandler : IHandler
{
    private const int PrisonerLiberationRelationReward = 10;

    private static readonly ILogger Logger = LogManager.GetLogger<PlayerCaptivityServerHandler>();
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;

    public PlayerCaptivityServerHandler(
        IObjectManager objectManager,
        INetwork network,
        IMessageBroker messageBroker,
        IPlayerManager playerManager)
    {
        this.objectManager = objectManager;
        this.network = network;
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;

        // ModInformation is evaluated per call (tests flip it per instance), so each handler
        // guards itself instead of gating the subscriptions here.
        messageBroker.Subscribe<PrisonerTaken>(Handle_PrisonerTaken);
        messageBroker.Subscribe<NetworkPlayerSurrendered>(Handle_NetworkPlayerSurrendered);
        messageBroker.Subscribe<NetworkEndPlayerCaptivityAttempted>(Handle_NetworkEndPlayerCaptivityAttempted);
        messageBroker.Subscribe<NetworkEndCaptivityAttempted>(Handle_NetworkEndCaptivityAttempted);
        messageBroker.Subscribe<NetworkPrisonerLiberationAttempted>(Handle_NetworkPrisonerLiberationAttempted);
        messageBroker.Subscribe<PlayerCaptivityEndedByServer>(Handle_PlayerCaptivityEndedByServer);
        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PrisonerTaken>(Handle_PrisonerTaken);
        messageBroker.Unsubscribe<NetworkPlayerSurrendered>(Handle_NetworkPlayerSurrendered);
        messageBroker.Unsubscribe<NetworkEndPlayerCaptivityAttempted>(Handle_NetworkEndPlayerCaptivityAttempted);
        messageBroker.Unsubscribe<NetworkEndCaptivityAttempted>(Handle_NetworkEndCaptivityAttempted);
        messageBroker.Unsubscribe<NetworkPrisonerLiberationAttempted>(Handle_NetworkPrisonerLiberationAttempted);
        messageBroker.Unsubscribe<PlayerCaptivityEndedByServer>(Handle_PlayerCaptivityEndedByServer);
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
    }

    /// <summary>
    /// Runs from the <see cref="TakePrisonerAction.ApplyInternal"/> postfix, after the native capture
    /// applied with patches live (hero state → Prisoner, member-roster removal, prison-roster add —
    /// each replicating to the clients as its own message, with
    /// <see cref="Hero.PartyBelongedToAsPrisoner"/> auto-synced). Only the coop-specific extras happen
    /// here: the player party's surviving companion heroes and remaining troops are recorded as prisoners
    /// of the captor (BR-061), the emptied rosters keep native
    /// <see cref="MapEvent.CaptureDefeatedPartyMembers"/> from re-processing or scattering them, and the
    /// party is parked until captivity ends. The park happens first — capturing a companion re-enters this
    /// handler (see <see cref="CaptureCompanionHeroes"/>), and the IsActive guard is the re-entrancy stop.
    /// </summary>
    private void Handle_PrisonerTaken(MessagePayload<PrisonerTaken> payload)
    {
        if (ModInformation.IsClient) return;

        var hero = payload.What.PrisonerHero;
        // PrisonerTaken is published from the TakePrisonerAction.ApplyInternal postfix, so native already
        // cleared hero.PartyBelongedTo and set PartyBelongedToAsPrisoner. The party the hero was captured
        // from therefore has to come from the message, not from the (now-null) hero.PartyBelongedTo.
        var playerParty = payload.What.PrisonerParty;

        PlayerCaptivityLogger.Debug("Handle_PrisonerTaken: hero={HeroId} party={PartyId} captor={CaptorId}",
            hero?.StringId, playerParty?.StringId, payload.What.CapturerParty?.MobileParty?.StringId);

        // Only player heroes need coop-specific handling; native TakePrisonerAction covers AI heroes.
        if (playerParty?.IsPlayerParty() != true)
        {
            PlayerCaptivityLogger.Debug("Handle_PrisonerTaken: skipping, captured party is not a player party");
            return;
        }

        // Guard against re-processing an already-parked party (a repeated capture, or one already freed).
        if (!playerParty.IsActive)
        {
            PlayerCaptivityLogger.Debug("Handle_PrisonerTaken: skipping, player party {PartyId} is already parked", playerParty.StringId);
            return;
        }

        // Park the party FIRST: capturing a companion below re-enters this handler through the
        // TakePrisonerAction postfix (a companion's PartyBelongedTo IS this same player party, so the
        // prefix snapshots it and the postfix publishes another PrisonerTaken), and the IsActive guard
        // above is what short-circuits that nested pass — running it before the guard state is set would
        // double the troop transfer and re-empty the rosters. MobileParty.IsActive is a plain flag with
        // no native side effects, so parking before the roster work only changes coop-internal ordering;
        // the captivity-end flow reactivates the party.
        playerParty.IsActive = false;

        // BR-061: surviving companion heroes riding in the surrendered party become prisoners of the
        // captor through the same TakePrisonerAction that captured the leader, BEFORE the rosters are
        // emptied below (which would silently discard them). Each capture runs with patches live, so its
        // side effects replicate exactly like the leader's did.
        CaptureCompanionHeroes(playerParty.MemberRoster, payload.What.CapturerParty);

        // BR-061: the surrendered party's remaining troops become prisoners of the captor. Transfer them
        // into the captor's prison roster BEFORE the rosters are emptied below; the additions run with
        // patches live, so they replicate to the clients the same way the removals do.
        TransferTroopsToCaptorPrisonRoster(playerParty.MemberRoster, payload.What.CapturerParty);

        // Empty the parked party's rosters so native post-battle processing cannot scatter or re-process
        // them. Empty by each element's ACTUAL current count rather than TroopRoster.Clear(): the native
        // TakePrisonerAction (run by Prefix_CaptureDefeatedPartyMembers) already removed the captured
        // hero, leaving a depleted element that Clear() subtracts AGAIN, driving the member count to -1
        // (the live "captured party roster goes negative" bug). Removing by the real count can never fall
        // below zero. The removals run with patches live, so each replicates to the clients. Note: heroes
        // the surrendered party itself HELD captive (its prison roster) are discarded here, not
        // transferred — out of BR-061's scope, which covers the surrendered party's own heroes and troops.
        EmptyRoster(playerParty.MemberRoster);
        EmptyRoster(playerParty.PrisonRoster);
        RemoveVisual(playerParty);
        if (playerParty.LeaderHero != null)
            playerParty.ChangePartyLeader(null);
    }

    /// <summary>
    /// Takes every surviving companion hero still riding in the surrendered party's member roster prisoner
    /// (BR-061 "heroes" clause) through the real <see cref="TakePrisonerAction"/> — the same action that
    /// captured the leader — so each companion gets proper hero captivity state, with the member-roster
    /// removal, the captor's prison-roster add and the auto-synced <see cref="Hero.PartyBelongedToAsPrisoner"/>
    /// each replicating to the clients. Wounded companions are still captured; a companion killed in THIS
    /// battle is never captured, and one already a prisoner is not captured again.
    /// MUST run after the party is parked: each capture re-publishes <see cref="PrisonerTaken"/> for this
    /// same party (the companion's <see cref="Hero.PartyBelongedTo"/> is the player party), and the IsActive
    /// guard in <see cref="Handle_PrisonerTaken"/> is what short-circuits that nested pass.
    /// <para>
    /// Aliveness alone is NOT a sufficient dead-check here: during an active map event native
    /// <see cref="KillCharacterAction"/> defers the kill (it only stamps a <see cref="Hero.DeathMark"/> and
    /// returns while the victim's party still has a <see cref="MapEvent"/>), so a hero killed in this very
    /// battle still reports <see cref="Hero.IsAlive"/> == true. The battle DeathMark is the reliable
    /// dead-signal, matching native <c>MapEvent.CaptureDefeatedPartyMembers</c> and the coop
    /// <c>MapEventResultsInterface</c> reimplementation — see <see cref="HasBattleDeathMark"/>.
    /// </para>
    /// </summary>
    private static void CaptureCompanionHeroes(TroopRoster memberRoster, PartyBase captor)
    {
        if (memberRoster == null || captor == null) return;

        // Snapshot first: each TakePrisonerAction removes its hero's element from this same roster.
        var companions = new List<Hero>();
        for (int i = 0; i < memberRoster.Count; i++)
        {
            var element = memberRoster.GetElementCopyAtIndex(i);
            if (element.Character?.IsHero != true) continue;

            // A depleted hero element (e.g. the captured leader's leftover) is not a live member.
            if (element.Number <= 0) continue;

            var companion = element.Character.HeroObject;
            // A companion killed in this battle is skipped: its death is deferred to a DeathMark while the map
            // event is active, so IsAlive is still true here (see HasBattleDeathMark). Aliveness alone would
            // capture a dead companion — check the DeathMark too. A wounded (not dead) companion has no battle
            // death mark and is still captured.
            if (companion == null || !companion.IsAlive || HasBattleDeathMark(companion) || companion.IsPrisoner) continue;

            companions.Add(companion);
        }

        foreach (var companion in companions)
        {
            PlayerCaptivityLogger.Debug("CaptureCompanionHeroes: capturing companion {HeroId} for captor {CaptorId}",
                companion.StringId, captor.MobileParty?.StringId);
            TakePrisonerAction.Apply(captor, companion);
        }
    }

    /// <summary>
    /// True when <paramref name="hero"/> was killed in the current battle. During an active map event native
    /// <see cref="KillCharacterAction"/> defers the kill: <c>ApplyInternal</c> only records a
    /// <see cref="Hero.DeathMark"/> and returns while the victim's party still has a <see cref="MapEvent"/>, so
    /// a hero killed in THIS battle still reports <see cref="Hero.IsAlive"/> == true when the surrender is
    /// processed. The battle death marks are therefore the reliable "is dead" signal at capture time — exactly
    /// what native <c>MapEvent.CaptureDefeatedPartyMembers</c> and the coop <c>MapEventResultsInterface</c>
    /// reimplementation gate on (DiedInBattle / DiedInLabor). A merely-wounded hero carries no battle death
    /// mark, so it stays capturable.
    /// </summary>
    private static bool HasBattleDeathMark(Hero hero)
        => hero.DeathMark == KillCharacterAction.KillCharacterActionDetail.DiedInBattle
        || hero.DeathMark == KillCharacterAction.KillCharacterActionDetail.DiedInLabor;

    /// <summary>
    /// Records the surrendered party's remaining regular troops as prisoners of the captor (BR-061): each
    /// non-hero member element is added to the captor's prison roster, wounded staying wounded. Heroes are
    /// excluded — a hero capture must go through <see cref="TakePrisonerAction"/>, which manages the hero
    /// state a raw roster add would bypass (the leader was captured natively; companions go through
    /// <see cref="CaptureCompanionHeroes"/>). The subsequent <see cref="EmptyRoster"/> removes the source
    /// elements, so native post-battle capture finds nothing to double-process.
    /// </summary>
    private static void TransferTroopsToCaptorPrisonRoster(TroopRoster memberRoster, PartyBase captor)
    {
        if (memberRoster == null || captor?.PrisonRoster == null) return;

        for (int i = 0; i < memberRoster.Count; i++)
        {
            var element = memberRoster.GetElementCopyAtIndex(i);
            if (element.Character == null || element.Character.IsHero) continue;

            int number = Math.Max(element.Number, 0);
            if (number == 0) continue;

            captor.PrisonRoster.AddToCounts(element.Character, number, false, Math.Max(element.WoundedNumber, 0), 0, true);
        }
    }

    /// <summary>
    /// Empties a roster to exactly zero by removing each element by its actual current count, then dropping the
    /// depleted entries. Unlike <see cref="TroopRoster.Clear"/>, this can never drive a count negative even when
    /// an earlier roster mutation left a depleted element behind.
    /// </summary>
    private static void EmptyRoster(TroopRoster roster)
    {
        if (roster == null) return;

        for (int i = roster.Count - 1; i >= 0; i--)
        {
            var element = roster.GetElementCopyAtIndex(i);
            int removeNumber = Math.Max(element.Number, 0);
            if (removeNumber > 0 || element.WoundedNumber > 0)
                roster.AddToCounts(element.Character, -removeNumber, false, -element.WoundedNumber, 0, true);
        }

        roster.RemoveZeroCounts();
    }

    /// <summary>
    /// A client surrendered (its <see cref="TaleWorlds.CampaignSystem.Encounters.PlayerEncounter"/>
    /// surrender is blocked locally). While healthy allies remain on the surrenderer's side, only the
    /// surrendering party is captured (<see cref="TakePrisonerAction"/> — its postfix drives
    /// <see cref="Handle_PrisonerTaken"/> for the companions/troops) and removed from the event, and the
    /// battle continues without it. Only when no other party on the side can still fight does the whole
    /// side surrender (<see cref="MapEvent.DoSurrender"/> — native semantics), which resolves the battle
    /// and captures the player hero through the finalize path.
    /// </summary>
    private void Handle_NetworkPlayerSurrendered(MessagePayload<NetworkPlayerSurrendered> payload)
    {
        if (ModInformation.IsClient) return;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging(payload.What.MapEventId, out MapEvent mapEvent)) return;
            if (!objectManager.TryGetObjectWithLogging(payload.What.PlayerParty, out MobileParty playerParty)) return;

            PlayerCaptivityLogger.Debug("Handle_NetworkPlayerSurrendered: applying surrender for party={PartyId} in mapEvent={MapEventId}",
                playerParty.StringId, payload.What.MapEventId);

            // While a live mission or an auto-resolve simulation owns this event, a surrender would
            // conclude the battle under the players resolving it — refuse it until the claim releases
            // (mission instance emptied / event finalized). The client menu already refuses the option
            // while claimed (BattleModeEncounterOptionsPatch); this is the authoritative backstop for
            // the race where the claim lands between the client's menu refresh and its click.
            if (ServerBattleModeArbiter.IsClaimed(payload.What.MapEventId))
            {
                Logger.Information("[PvPEncounterClose] Refused surrender of {PartyId} in {MapEventId}: the event is claimed by an active mission/simulation",
                    playerParty.StringId, payload.What.MapEventId ?? "<none>");
                return;
            }

            if (!objectManager.TryGetIdWithLogging(playerParty.Party, out var surrenderedPartyId)) return;

            // While OTHER parties on the surrenderer's side can still fight, DoSurrender below would end
            // the battle for all of them (it marks the whole side surrendered and hands the other side
            // the win). Capture just the surrendering party instead and let the battle continue.
            var side = playerParty.Party.MapEventSide;
            var hasHealthyAllies = side != null &&
                side.Parties.Any(p => p.Party != playerParty.Party && p.Party?.NumberOfHealthyMembers > 0);
            if (hasHealthyAllies)
            {
                ApplyPartialSurrender(mapEvent, playerParty, surrenderedPartyId);
                return;
            }

            var playerPartyIds = MapEventPlayerPartyCollector.CollectPartyIds(mapEvent, objectManager);

            Logger.Information("[PvPEncounterClose] Server sending immediate surrender close: partyIds=[{PartyIds}] surrenderedPartyId={SurrenderedPartyId} mapEventId={MapEventId}",
                string.Join(",", playerPartyIds),
                surrenderedPartyId ?? "<none>",
                payload.What.MapEventId ?? "<none>");
            PvpEncounterCloseSender.Send(network, messageBroker, this, playerPartyIds, surrenderedPartyId, payload.What.MapEventId);
            mapEvent.DoSurrender(playerParty.Party.Side);
            messageBroker.Publish(this, new MapEventConcluded(payload.What.MapEventId, playerPartyIds, surrenderedPartyId));
        }, blocking: true, context: nameof(Handle_NetworkPlayerSurrendered));
    }

    /// <summary>
    /// Surrenders ONLY <paramref name="playerParty"/> out of a battle its side keeps fighting: the party is
    /// removed from the event (explicit broadcast — single-party removal does not auto-replicate; applying
    /// it closes the surrenderer's own encounter menu, see <c>BattleJoinLeaveHandler.ApplyNetworkLeave</c>)
    /// and its hero is captured by the opposing side's leader as a plain out-of-battle capture. The capture
    /// runs with patches live, so its side effects replicate and its postfix-published
    /// <see cref="PrisonerTaken"/> drives <see cref="Handle_PrisonerTaken"/> (park, companions, troop
    /// transfer); the owning client then enters captivity from the synced state. No
    /// <see cref="MapEventConcluded"/>: the event lives on for the allies.
    /// </summary>
    private void ApplyPartialSurrender(MapEvent mapEvent, MobileParty playerParty, string surrenderedPartyId)
    {
        if (!TryGetCaptorForPartialSurrender(mapEvent, playerParty.Party.Side, out var captorParty))
        {
            // No enemy party can hold prisoners — the battle is effectively decided and about to resolve
            // through its own paths; a surrender into a spent side has nothing coherent to do.
            Logger.Warning("Refused partial surrender of {PartyId}: no opposing party remains to take prisoners", surrenderedPartyId);
            return;
        }

        if (!TryGetPlayerHeroOfParty(playerParty, out var playerHero))
        {
            Logger.Error("Refused partial surrender of {PartyId}: no registered player hero resolves for it", surrenderedPartyId);
            return;
        }

        Logger.Information("Applying partial surrender: party={PartyId} captor={CaptorId} — healthy allies keep fighting the battle",
            surrenderedPartyId, captorParty.MobileParty?.StringId ?? "<settlement>");

        // Out of the battle first, so the capture below is a plain out-of-battle capture.
        playerParty.Party.MapEventSide = null;
        network.SendAll(new NetworkPartyLeftBattle(surrenderedPartyId));

        TakePrisonerAction.Apply(captorParty, playerHero);
    }

    /// <summary>
    /// Enemy party to hold a partial surrender's prisoners: the opposing side's leader while it can still
    /// fight, else the side's first party with healthy members, else the leader regardless. False only when
    /// no opposing party remains at all.
    /// </summary>
    private static bool TryGetCaptorForPartialSurrender(MapEvent mapEvent, BattleSideEnum surrenderingSide, out PartyBase captorParty)
    {
        captorParty = null;

        var enemySide = mapEvent.GetMapEventSide(mapEvent.GetOtherSide(surrenderingSide));
        if (enemySide == null) return false;

        var leader = enemySide.LeaderParty;
        if (leader != null && leader.NumberOfHealthyMembers > 0)
        {
            captorParty = leader;
            return true;
        }

        captorParty = enemySide.Parties.FirstOrDefault(p => p.Party?.NumberOfHealthyMembers > 0)?.Party ?? leader;
        return captorParty != null;
    }

    /// <summary>
    /// Resolves the player hero registered for <paramref name="playerParty"/>. A player party's component
    /// leader is not reliably set on the server, so the player registry is the source of truth (the inverse
    /// of <see cref="TryGetPlayerParty"/>). The registry keys players by the MOBILE party's id, which is
    /// distinct from the <see cref="PartyBase"/> id used on the battle-leave wire.
    /// </summary>
    private bool TryGetPlayerHeroOfParty(MobileParty playerParty, out Hero playerHero)
    {
        playerHero = null;

        if (!objectManager.TryGetId(playerParty, out var mobilePartyId)) return false;

        foreach (var player in playerManager.Players)
        {
            if (player.MobilePartyId == mobilePartyId)
                return objectManager.TryGetObject(player.HeroId, out playerHero);
        }

        return false;
    }

    /// <summary>
    /// A client chose to release another hero. Apply the vanilla action on the server so its
    /// patches and synchronized side effects remain authoritative.
    /// </summary>
    private void Handle_NetworkEndCaptivityAttempted(MessagePayload<NetworkEndCaptivityAttempted> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.PrisonerId, out var prisoner)) return;
            if (!prisoner.IsPrisoner) return;

            Hero facilitator = null;
            if (data.FacilitatorId != null && !objectManager.TryGetObjectWithLogging(data.FacilitatorId, out facilitator)) return;

            PlayerCaptivityLogger.Debug("Handle_NetworkEndCaptivityAttempted (server): prisoner={HeroId} detail={Detail} facilitator={FacilitatorId}",
                prisoner.StringId, data.Detail, facilitator?.StringId);

            EndCaptivityAction.ApplyInternal(prisoner, data.Detail, facilitator, data.ShowNotification);
        }, context: nameof(Handle_NetworkEndCaptivityAttempted));
    }

    private void Handle_NetworkPrisonerLiberationAttempted(MessagePayload<NetworkPrisonerLiberationAttempted> payload)
    {
        if (ModInformation.IsClient) return;

        if (!(payload.Who is NetPeer peer) || !playerManager.TryGetPlayer(peer, out var player))
        {
            Logger.Error("Received {Message} without a registered player peer", nameof(NetworkPrisonerLiberationAttempted));
            return;
        }

        string playerHeroId = player.HeroId;
        string prisonerId = payload.What.PrisonerId;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(playerHeroId, out var playerHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(prisonerId, out var prisoner)) return;
            if (!prisoner.IsPrisoner) return;

            PlayerCaptivityLogger.Debug(
                "Handle_NetworkPrisonerLiberationAttempted (server): player={PlayerHeroId} prisoner={PrisonerId}",
                playerHero.StringId,
                prisoner.StringId);

            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                playerHero,
                prisoner,
                PrisonerLiberationRelationReward);
        }, context: nameof(Handle_NetworkPrisonerLiberationAttempted));
    }

    /// <summary>
    /// A client requests release from captivity (ransom paid, escape, captor let them go).
    /// Re-implements native <see cref="PlayerCaptivity"/>.EndCaptivityInternal for a remote player
    /// hero, then confirms to the requesting client so it can leave the captivity menus.
    /// </summary>
    private void Handle_NetworkEndPlayerCaptivityAttempted(MessagePayload<NetworkEndPlayerCaptivityAttempted> payload)
    {
        if (ModInformation.IsClient) return;

        var heroId = payload.What.PlayerHeroId;
        var partyId = payload.What.PlayerPartyId;
        var facilitatorId = payload.What.FacilitatorId;
        var detail = payload.What.Detail;
        var ransomAmount = payload.What.RansomAmount;
        var releasePosition = payload.What.PlayerPartyPosition;
        var peer = payload.Who as NetPeer;

        // The release touches party/roster game state the main-thread tick also touches, so defer the
        // apply to the game loop; resolve the object ids inside the lambda so a deferred create that lands
        // first is visible, and send the reply inside the lambda after the release runs so the client only
        // leaves the captivity menus once the server has actually applied it.
        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(heroId, out var playerHero))
                    return;
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(partyId, out var playerParty))
                    return;

                Hero facilitator = null;
                if (facilitatorId != null && !objectManager.TryGetObjectWithLogging(facilitatorId, out facilitator))
                    return;

                PlayerCaptivityLogger.Debug("Handle_NetworkEndPlayerCaptivityAttempted (server): hero={HeroId} party={PartyId} detail={Detail} facilitator={FacilitatorId}",
                    playerHero.StringId, playerParty.StringId, detail, facilitator?.StringId);

                ReleasePlayerFromCaptivity(playerHero, playerParty, detail, facilitator, releasePosition);

                if (detail == EndCaptivityDetail.Ransom && ransomAmount != 0)
                {
                    GiveGoldAction.ApplyBetweenCharacters(playerHero, null, ransomAmount, false);
                }

                network.Send(peer, new NetworkPlayerCaptivityEnded());
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(NetworkEndPlayerCaptivityAttempted));
            }
        }, blocking: true);
    }

    /// <summary>
    /// The server itself freed a player (client) hero — typically because the captor party was defeated in
    /// battle (native <see cref="MapEvent.LootDefeatedPartyPrisoners"/> →
    /// <see cref="EndCaptivityAction.ApplyByReleasedAfterBattle"/>), but also AI ransoms and peace releases.
    /// Unlike the client-requested path there is no request to answer; the owning client leaves the
    /// captivity menus on its own when the cleared <see cref="Hero.PartyBelongedToAsPrisoner"/> syncs to it
    /// (<see cref="PlayerCaptivityClientHandler"/>).
    /// </summary>
    private void Handle_PlayerCaptivityEndedByServer(MessagePayload<PlayerCaptivityEndedByServer> payload)
    {
        if (ModInformation.IsClient) return;

        var playerHero = payload.What.PrisonerHero;
        if (playerHero == null) return;

        if (!TryGetPlayerParty(playerHero, out var playerParty))
        {
            Logger.Error("Could not resolve a player party for released hero {HeroId}; cannot restore it", playerHero.StringId);
            return;
        }

        PlayerCaptivityLogger.Debug("Handle_PlayerCaptivityEndedByServer: hero={HeroId} party={PartyId} detail={Detail}",
            playerHero.StringId, playerParty.StringId, payload.What.Detail);

        var captorParty = playerHero.PartyBelongedToAsPrisoner;
        var releasePosition = payload.What.HasReleasePosition
            ? payload.What.ReleasePosition
            : GetReleasePosition(captorParty, playerParty.Position);

        ReleasePlayerFromCaptivity(playerHero, playerParty, payload.What.Detail, payload.What.Facilitator, releasePosition);
    }

    /// <summary>
    /// Server-authoritative release of a player (client) hero from captivity, shared by the client-requested
    /// and server-initiated paths. Restores the deactivated player party to the map and clears the captivity
    /// state — which auto-syncs to the clients through <see cref="Hero.PartyBelongedToAsPrisoner"/>.
    /// Re-implements native <see cref="PlayerCaptivity"/>.EndCaptivityInternal for a hero that is not this
    /// instance's main hero; the menu/encounter cleanup the native version does happens on the owning client
    /// instead (<see cref="PlayerCaptivityClientHandler"/>).
    /// </summary>
    private void ReleasePlayerFromCaptivity(Hero playerHero, MobileParty playerParty, EndCaptivityDetail detail, Hero facilitator, CampaignVec2 releasePosition)
    {
        // Guard against re-processing an already-ended captivity: a client release request can race a
        // server-initiated release, and a second pass would re-add the hero to the member roster,
        // doubling the troop count. The captor reference is the captivity's source of truth — it is
        // still set on every legitimate entry (the EndCaptivityAction prefix intercepts before native
        // clears anything, including a death in captivity) and cleared below on the first pass.
        if (playerHero.PartyBelongedToAsPrisoner == null)
        {
            PlayerCaptivityLogger.Debug("ReleasePlayerFromCaptivity: skipping, hero {HeroId} is no longer captive", playerHero.StringId);
            return;
        }

        // Snapshot the captor before the release: clearing the captivity below nulls
        // PartyBelongedToAsPrisoner, and a captor defeated in battle may already be inactive.
        PartyBase captorParty = playerHero.PartyBelongedToAsPrisoner;
        IFaction capturerFaction = captorParty?.MapFaction;

        if (playerHero.IsAlive)
        {
            playerHero.ChangeState(Hero.CharacterStates.Active);
            playerParty.AddElementToMemberRoster(playerHero.CharacterObject, 1, true);
            playerParty.ChangePartyLeader(playerHero);
        }
        if (playerHero.CurrentSettlement != null)
        {
            if (playerHero.IsAlive)
            {
                LeaveSettlementAction.ApplyForParty(playerParty);
            }
            else
            {
                LeaveSettlementAction.ApplyForCharacterOnly(playerHero);
            }
        }

        // Clear the captivity. Removing the hero from the captor's prison roster clears
        // PartyBelongedToAsPrisoner via the engine hook; do this regardless of whether the captor is still
        // active, since a captor defeated in battle may already be inactive. If the roster no longer holds
        // the hero, null it directly so the cleared state still auto-syncs to the owning client.
        if (captorParty != null && captorParty.PrisonRoster.Contains(playerHero.CharacterObject))
        {
            captorParty.PrisonRoster.RemoveTroop(playerHero.CharacterObject);
        }
        if (playerHero.PartyBelongedToAsPrisoner != null)
        {
            playerHero.PartyBelongedToAsPrisoner = null;
        }

        // Separate the freed party from a still-active mobile captor (mirrors native). A defeated/inactive
        // captor is skipped — there's nothing to disengage from, and navigating around a destroyed party
        // would be meaningless.
        if (captorParty?.IsActive == true && captorParty.IsMobile && !captorParty.MobileParty.IsCurrentlyAtSea)
        {
            playerParty.TeleportPartyToOutSideOfEncounterRadius();
        }

        if (captorParty != null && captorParty.IsSettlement)
        {
            playerParty.DisembarkToPosition(captorParty.Settlement.GatePosition);
        }
        else if (captorParty != null && captorParty.IsMobile)
        {
            playerParty.IsCurrentlyAtSea = captorParty.MobileParty.IsCurrentlyAtSea;
        }
        if (facilitator != null && detail != EndCaptivityDetail.Death)
        {
            StringHelpers.SetCharacterProperties("FACILITATOR", facilitator.CharacterObject, null, false);
            StringHelpers.SetCharacterProperties("PRISONER", playerHero.CharacterObject, null, false);
            MBInformationManager.AddQuickInformation(new TextObject("{=xPuSASof}{FACILITATOR.NAME} paid a ransom and freed {PRISONER.NAME} from captivity."));
        }
        CampaignEventDispatcher.Instance.OnHeroPrisonerReleased(playerHero, captorParty, capturerFaction, detail, true);

        if (playerHero.IsAlive)
        {
            playerParty.Position = releasePosition;
            playerParty.IsActive = true;
            playerParty.IgnoreForHours(4);
            if (captorParty?.MobileParty?.IsActive == true)
            {
                // Vanilla protects MainParty from its former captor for 12 hours; apply it to this client party.
                DefaultMobilePartyAIModelPatches.PreventAttacksUntil(
                    captorParty.MobileParty,
                    playerParty,
                    CampaignTime.HoursFromNow(12));
            }
            playerParty.Party.SetAsCameraFollowParty();
            playerParty.SetMoveModeHold();
            // SetMoveModeHold only resets the AI behavior, not the navigation mode, so the freed party
            // would keep whatever Party-mode target it had at capture (its old captor, or a party that
            // was destroyed and deserialized to null after a save/reload). Clear it so the released party
            // actually holds and obeys the player's next move order instead of being stuck or throwing.
            playerParty.ResetNavigationToHold();

            // Native grants the released hero captivity XP through Campaign.Current.PlayerCaptivity,
            // which on the server tracks the host hero — using it here would XP the wrong hero.
            // TODO grant the released client hero its captivity XP.
            if (playerHero == Hero.MainHero)
            {
                SkillLevelingManager.OnMainHeroReleasedFromCaptivity(PlayerCaptivity.CaptivityStartTime.ElapsedHoursUntilNow);
            }

            if (!playerParty.IsCurrentlyAtSea)
            {
                playerParty.Party.UpdateVisibilityAndInspected(playerParty.Position);
            }
            SyncReleasePosition(playerParty, releasePosition);

            // Rebuild the map mesh after the roster/leader/position are restored, so the freed party's map
            // figure reflects its (re-mounted) state rather than the stale on-foot captive mesh.
            playerParty.Party.SetVisualAsDirty();
            RecreateVisual(playerParty);
        }
    }

    private CampaignVec2 GetReleasePosition(PartyBase captorParty, CampaignVec2 fallbackPosition)
    {
        if (captorParty == null)
            return fallbackPosition;

        if (captorParty.IsSettlement)
            return captorParty.Settlement.GatePosition;

        if (captorParty.IsMobile)
            return captorParty.MobileParty.Position;

        return captorParty.Position;
    }

    private void SyncReleasePosition(MobileParty playerParty, CampaignVec2 releasePosition)
    {
        if (!objectManager.TryGetIdWithLogging(playerParty, out string playerPartyId)) return;

        network.SendAll(new NetworkPlayerCaptivityReleasePositionSet(playerPartyId, releasePosition));
    }

    private void RemoveVisual(MobileParty party)
    {
        var partyVisual = party.Party.GetPartyVisual();
        if (partyVisual == null) return;
        if (!objectManager.TryGetIdWithLogging(partyVisual, out string visualId)) return;

        objectManager.Remove(partyVisual);

        using (new AllowedThread())
        {
            MobilePartyVisualManager.Current?.RemovePartyVisualForParty(party);
        }

        network.SendAll(new NetworkDestroyPartyVisual(visualId));
    }

    private void RecreateVisual(MobileParty party)
    {
        RemoveVisual(party);

        if (!objectManager.TryGetIdWithLogging(party, out string mobilePartyId)) return;

        using (new AllowedThread())
        {
            party.CreateNewPartyVisual();
        }

        var partyVisual = party.Party.GetPartyVisual();
        if (partyVisual == null)
        {
            Logger.Error("CreateNewPartyVisual did not produce a visual for party {PartyId}", party.StringId);
            return;
        }

        if (!objectManager.AddNewObject(partyVisual, out var visualId))
        {
            Logger.Error("Failed to register recreated visual for party {PartyId}", party.StringId);
            return;
        }

        network.SendAll(new NetworkCreatePartyVisual(visualId, mobilePartyId));
    }

    /// <summary>
    /// Resolves the <see cref="MobileParty"/> registered to the player that owns <paramref name="hero"/>.
    /// </summary>
    private bool TryGetPlayerParty(Hero hero, out MobileParty playerParty)
    {
        playerParty = null;

        if (!objectManager.TryGetId(hero, out var heroId))
            return false;

        foreach (var player in playerManager.Players)
        {
            if (player.HeroId == heroId)
                return objectManager.TryGetObject(player.MobilePartyId, out playerParty);
        }

        return false;
    }

    /// <summary>
    /// Keeps captive players' parties at their captor's position; the server-side replacement for
    /// native <see cref="PlayerCaptivity"/>.Update, which only handles the local main hero.
    /// Positions are server-authoritative and replicate to the clients through party movement sync.
    /// </summary>
    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        if (ModInformation.IsClient) return;

        foreach (var (hero, mobileParty) in PlayerHeros())
        {
            if (!hero.IsPrisoner) continue;

            var captorParty = hero.PartyBelongedToAsPrisoner;

            if (captorParty == null) continue;

            mobileParty.Position = captorParty.Position;
        }
    }

    private IEnumerable<(Hero, MobileParty)> PlayerHeros()
    {
        foreach (var player in playerManager.Players)
        {
            if (objectManager.TryGetObject<Hero>(player.HeroId, out var hero) &&
                objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var mobileParty))
            {
                yield return (hero, mobileParty);
            }
        }
    }
}
