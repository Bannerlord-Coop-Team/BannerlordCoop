using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using Helpers;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
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
/// <item><see cref="CampaignTick"/> — keep captive players' parties glued to their captor
/// (the server-side replacement for native <see cref="PlayerCaptivity"/>.Update).</item>
/// </list>
/// The client counterpart is <see cref="PlayerCaptivityClientHandler"/>.
/// </summary>
internal class PlayerCaptivityServerHandler : IHandler
{
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
        messageBroker.Subscribe<PlayerCaptivityEndedByServer>(Handle_PlayerCaptivityEndedByServer);
        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PrisonerTaken>(Handle_PrisonerTaken);
        messageBroker.Unsubscribe<NetworkPlayerSurrendered>(Handle_NetworkPlayerSurrendered);
        messageBroker.Unsubscribe<NetworkEndPlayerCaptivityAttempted>(Handle_NetworkEndPlayerCaptivityAttempted);
        messageBroker.Unsubscribe<PlayerCaptivityEndedByServer>(Handle_PlayerCaptivityEndedByServer);
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
    }

    /// <summary>
    /// Runs from the <see cref="TakePrisonerAction.ApplyInternal"/> prefix, before the native body.
    /// Native does the canonical capture right after this returns (hero state → Prisoner, add to the
    /// captor's prison roster, which sets <see cref="Hero.PartyBelongedToAsPrisoner"/> and replicates
    /// it to the clients). Only the coop-specific extras happen here: the player party's troops are
    /// forfeited so native <see cref="MapEvent.CaptureDefeatedPartyMembers"/> cannot re-process or
    /// scatter them, and the party is parked until captivity ends.
    /// </summary>
    private void Handle_PrisonerTaken(MessagePayload<PrisonerTaken> payload)
    {
        if (ModInformation.IsClient) return;

        var hero = payload.What.PrisonerHero;
        // TakePrisonerActionPatches runs native TakePrisonerAction.Apply before publishing this, which
        // already cleared hero.PartyBelongedTo and set PartyBelongedToAsPrisoner. The party the hero was
        // captured from therefore has to come from the message, not from the (now-null) hero.PartyBelongedTo.
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

        // Park the now-leaderless player party so native post-battle processing cannot scatter or destroy
        // it; the captivity-end flow reactivates it.
        playerParty.MemberRoster.Clear();
        playerParty.PrisonRoster.Clear();
        playerParty.IsActive = false;
        playerParty.ChangePartyLeader(null);
    }

    /// <summary>
    /// A client surrendered (its <see cref="TaleWorlds.CampaignSystem.Encounters.PlayerEncounter"/>
    /// surrender is blocked locally). Resolve the battle on the server; the finalize path then
    /// captures the player hero through <see cref="MapEvent.CaptureDefeatedPartyMembers"/>.
    /// </summary>
    private void Handle_NetworkPlayerSurrendered(MessagePayload<NetworkPlayerSurrendered> payload)
    {
        if (ModInformation.IsClient) return;

        if (!objectManager.TryGetObjectWithLogging(payload.What.MapEventId, out MapEvent mapEvent)) return;
        if (!objectManager.TryGetObjectWithLogging(payload.What.PlayerParty, out MobileParty playerParty)) return;

        PlayerCaptivityLogger.Debug("Handle_NetworkPlayerSurrendered: applying surrender for party={PartyId} in mapEvent={MapEventId}",
            playerParty.StringId, payload.What.MapEventId);

        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                mapEvent.DoSurrender(playerParty.Party.Side);
                mapEvent.FinalizeEvent();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to surrender");
            }
        }, blocking: true);
    }

    /// <summary>
    /// A client requests release from captivity (ransom paid, escape, captor let them go).
    /// Re-implements native <see cref="PlayerCaptivity"/>.EndCaptivityInternal for a remote player
    /// hero, then confirms to the requesting client so it can leave the captivity menus.
    /// </summary>
    private void Handle_NetworkEndPlayerCaptivityAttempted(MessagePayload<NetworkEndPlayerCaptivityAttempted> payload)
    {
        if (ModInformation.IsClient) return;

        if (!objectManager.TryGetObjectWithLogging<Hero>(payload.What.PlayerHeroId, out var playerHero))
            return;
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(payload.What.PlayerPartyId, out var playerParty))
            return;

        Hero facilitator = null;
        if (payload.What.FacilitatorId != null && !objectManager.TryGetObjectWithLogging(payload.What.FacilitatorId, out facilitator))
            return;

        PlayerCaptivityLogger.Debug("Handle_NetworkEndPlayerCaptivityAttempted (server): hero={HeroId} party={PartyId} detail={Detail} facilitator={FacilitatorId}",
            playerHero.StringId, playerParty.StringId, payload.What.Detail, facilitator?.StringId);

        ReleasePlayerFromCaptivity(playerHero, playerParty, payload.What.Detail, facilitator, payload.What.PlayerPartyPosition);

        var message = new NetworkPlayerCaptivityEnded();
        network.Send(payload.Who as NetPeer, message);
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

        // The party was pinned to the captor while captive (Handle_CampaignTick), so its current position is
        // where the release happens.
        ReleasePlayerFromCaptivity(playerHero, playerParty, payload.What.Detail, payload.What.Facilitator, playerParty.Position);
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
            playerParty.IsActive = true;
            playerParty.Position = releasePosition;
            playerParty.IgnoreForHours(4);
            playerParty.Party.SetAsCameraFollowParty();
            playerParty.SetMoveModeHold();

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

            // Rebuild the map mesh after the roster/leader/position are restored, so the freed party's map
            // figure reflects its (re-mounted) state rather than the stale on-foot captive mesh.
            playerParty.Party.SetVisualAsDirty();
        }
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
