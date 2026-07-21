using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.PlayerCaptivityService.Handlers;

/// <summary>
/// Client side of player captivity. The client never decides captivity itself — it reacts to the
/// server-synced <see cref="Hero.PartyBelongedToAsPrisoner"/> of its own hero and forwards local
/// intents to the server:
/// <list type="bullet">
/// <item><see cref="PlayerCaptivityChanged"/> — the synced captor changed; show or leave the
/// captivity menus (this is what makes the capture screen appear).</item>
/// <item><see cref="PlayerSurrendered"/> — the local player surrendered; ask the server to resolve
/// the battle.</item>
/// <item><see cref="EndPlayerCaptivityAttempted"/> — a local captivity menu option ended captivity;
/// ask the server to release the hero.</item>
/// <item><see cref="EndCaptivityAttempted"/> — the player chose to release another hero; ask the
/// server to apply the release.</item>
/// <item><see cref="PrisonerLiberationAttempted"/> — the player liberated a prisoner through the
/// post-battle conversation; ask the server to apply the relation reward.</item>
/// <item><see cref="NetworkPlayerCaptivityEnded"/> — the server confirmed the release; leave the
/// captivity menus.</item>
/// </list>
/// The server counterpart is <see cref="PlayerCaptivityServerHandler"/>.
/// </summary>
internal class PlayerCaptivityClientHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerCaptivityClientHandler>();
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMessageBroker messageBroker;

    public PlayerCaptivityClientHandler(
        IObjectManager objectManager,
        INetwork network,
        IMessageBroker messageBroker)
    {
        this.objectManager = objectManager;
        this.network = network;
        this.messageBroker = messageBroker;

        // ModInformation is evaluated per call (tests flip it per instance), so each handler
        // guards itself instead of gating the subscriptions here.
        messageBroker.Subscribe<PlayerCaptivityChanged>(Handle_PlayerCaptivityChanged);
        messageBroker.Subscribe<PlayerSurrendered>(Handle_PlayerSurrendered);
        messageBroker.Subscribe<EndPlayerCaptivityAttempted>(Handle_EndPlayerCaptivityAttempted);
        messageBroker.Subscribe<EndCaptivityAttempted>(Handle_EndCaptivityAttempted);
        messageBroker.Subscribe<PrisonerLiberationAttempted>(Handle_PrisonerLiberationAttempted);
        messageBroker.Subscribe<NetworkPlayerCaptivityEnded>(Handle_NetworkPlayerCaptivityEnded);
        messageBroker.Subscribe<NetworkPlayerCaptivityReleasePositionSet>(Handle_NetworkPlayerCaptivityReleasePositionSet);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerCaptivityChanged>(Handle_PlayerCaptivityChanged);
        messageBroker.Unsubscribe<PlayerSurrendered>(Handle_PlayerSurrendered);
        messageBroker.Unsubscribe<EndPlayerCaptivityAttempted>(Handle_EndPlayerCaptivityAttempted);
        messageBroker.Unsubscribe<EndCaptivityAttempted>(Handle_EndCaptivityAttempted);
        messageBroker.Unsubscribe<PrisonerLiberationAttempted>(Handle_PrisonerLiberationAttempted);
        messageBroker.Unsubscribe<NetworkPlayerCaptivityEnded>(Handle_NetworkPlayerCaptivityEnded);
        messageBroker.Unsubscribe<NetworkPlayerCaptivityReleasePositionSet>(Handle_NetworkPlayerCaptivityReleasePositionSet);
    }

    /// <summary>
    /// The synced captor of the hero this client controls changed
    /// (<see cref="Hero.PartyBelongedToAsPrisoner"/> setter postfix). Mirrors what native
    /// <see cref="PlayerCaptivity"/>.StartCaptivityInternal / EndCaptivityInternal do to the local
    /// camera, captivity state and menus.
    /// </summary>
    private void Handle_PlayerCaptivityChanged(MessagePayload<PlayerCaptivityChanged> payload)
    {
        if (ModInformation.IsServer) return;

        var captorParty = payload.What.CaptorParty;

        GameThread.Run(() =>
        {
            if (Campaign.Current is null) return;

            if (captorParty == null)
            {
                ReleaseMainParty();
            }
            else
            {
                StartCaptivity(captorParty);
            }
        });
    }

    private static void StartCaptivity(PartyBase captorParty)
    {
        PlayerCaptivityLogger.Debug("Handle_PlayerCaptivityChanged: captor set to {CaptorId} (isSettlement={IsSettlement})",
            captorParty.IsMobile ? captorParty.MobileParty?.StringId : captorParty.Settlement?.StringId, captorParty.IsSettlement);

        using (new AllowedThread())
        {
            if (PlayerEncounter.Current != null)
            {
                PlayerEncounter.LeaveEncounter = true;
            }
        }

        var playerCaptivity = Campaign.Current.PlayerCaptivity;

        playerCaptivity._captivityStartTime = CampaignTime.Now;
        playerCaptivity._lastCheckTime = CampaignTime.Now;
        PlayerCaptivity.RandomNumber = MBRandom.RandomFloat;

        PartyBase.MainParty.UpdateVisibilityAndInspected(MobileParty.MainParty.Position, 0f);
        playerCaptivity._captorParty = captorParty;
        playerCaptivity._captorParty.SetAsCameraFollowParty();
        playerCaptivity._captorParty.UpdateVisibilityAndInspected(MobileParty.MainParty.Position, 0f);

        // Mirror native menu activation: switch when a menu is already open (e.g. the post-battle
        // encounter menu), otherwise activate a fresh one. A bare SwitchToMenu does nothing when no
        // menu is open, which left the player captive without a capture screen.
        var menuId = playerCaptivity._captorParty.IsSettlement ? "settlement_wait" : "prisoner_wait";
        if ((Game.Current.GameStateManager.ActiveState as MapState)?.AtMenu == true)
        {
            GameMenu.SwitchToMenu(menuId);
        }
        else
        {
            GameMenu.ActivateGameMenu(menuId);
        }
    }

    private static void ReleaseMainParty()
    {
        PlayerCaptivityLogger.Debug("Handle_PlayerCaptivityChanged: captor cleared, releasing main party");

        PartyBase.MainParty.UpdateVisibilityAndInspected(MobileParty.MainParty.Position, 0f);
        PartyBase.MainParty.SetAsCameraFollowParty();

        Campaign.Current.PlayerCaptivity._captorParty = null;

        // Leave the captivity menus/encounter now that we're free. This drives the client UI for
        // server-initiated releases (e.g. our captor was defeated), where — unlike a client-requested
        // release — there is no NetworkPlayerCaptivityEnded ack; we react to the synced captor clearing.
        if (PlayerEncounter.Current != null)
        {
            PlayerEncounter.LeaveSettlement();
        }
        if (PlayerEncounter.Current != null)
        {
            PlayerEncounter.Finish(true);
        }
        else if (Campaign.Current.CurrentMenuContext != null)
        {
            GameMenu.ExitToLast();
        }
    }

    /// <summary>
    /// The local player surrendered; the native surrender is blocked on clients
    /// (PlayerEncounterPatches), so forward it for the server to resolve the battle.
    /// </summary>
    private void Handle_PlayerSurrendered(MessagePayload<PlayerSurrendered> payload)
    {
        if (ModInformation.IsServer) return;

        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out string mapEventId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.PlayerParty, out string playerPartyId)) return;

        PlayerCaptivityLogger.Debug("Handle_PlayerSurrendered: requesting surrender of party={PartyId} in mapEvent={MapEventId}",
            playerPartyId, mapEventId);
        Logger.Information(
            "[PvPBattleEncounterTrace] Client submitting battle encounter surrender request; playerPartyId={PlayerPartyId} mapEventId={MapEventId} menu={Menu} encounter={Encounter} captive={Captive}",
            playerPartyId,
            mapEventId,
            Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "<none>",
            PlayerEncounter.Current != null,
            PlayerCaptivity.IsCaptive);

        network.SendAll(new NetworkPlayerSurrendered(playerPartyId, mapEventId));
    }

    /// <summary>
    /// A local conversation or screen released another hero; forward the intent to the server and
    /// remove that hero from the local post-battle queue so the conversation does not reopen.
    /// </summary>
    private void Handle_EndCaptivityAttempted(MessagePayload<EndCaptivityAttempted> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;

        if (!objectManager.TryGetIdWithLogging(data.Prisoner, out string prisonerId)) return;

        string facilitatorId = null;
        if (data.Facilitator != null && !objectManager.TryGetIdWithLogging(data.Facilitator, out facilitatorId)) return;

        network.SendAll(new NetworkEndCaptivityAttempted(prisonerId, data.Detail, facilitatorId, data.ShowNotification));

        PlayerEncounter.Current?._capturedAlreadyPrisonerHeroes?
            .RemoveAll(element => element.Character?.HeroObject == data.Prisoner);
    }

    private void Handle_PrisonerLiberationAttempted(MessagePayload<PrisonerLiberationAttempted> payload)
    {
        if (ModInformation.IsServer) return;

        if (!objectManager.TryGetIdWithLogging(payload.What.Prisoner, out string prisonerId)) return;

        network.SendAll(new NetworkPrisonerLiberationAttempted(prisonerId));
    }

    /// <summary>
    /// A captivity menu option ended captivity locally (EndCaptivityAction is intercepted on
    /// clients); forward the request and optimistically clear the local captivity state. The server
    /// confirms with <see cref="NetworkPlayerCaptivityEnded"/>.
    /// </summary>
    private void Handle_EndPlayerCaptivityAttempted(MessagePayload<EndPlayerCaptivityAttempted> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;

        PlayerCaptivityLogger.Debug("Handle_EndPlayerCaptivityAttempted (client): hero={HeroId} detail={Detail} facilitator={FacilitatorId}",
            data.PlayerHero?.StringId, data.Detail, data.Facilitator?.StringId);

        // Reading the captivity state and the main party, then forwarding the request and clearing the
        // local captivity state, all touch game state the main-thread tick also touches, so defer the
        // apply to the game loop. Ids are resolved inside the lambda so a deferred create that lands
        // first is visible, and the forward to the server goes out from the main thread after the read.
        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetIdWithLogging(data.PlayerHero, out string heroId)) return;

                var playerParty = MobileParty.MainParty;
                if (!objectManager.TryGetIdWithLogging(playerParty, out string partyId)) return;

                string facilitatorId = null;
                if (data.Facilitator != null && !objectManager.TryGetIdWithLogging(data.Facilitator, out facilitatorId)) return;
                int ransomAmount = Campaign.Current.PlayerCaptivity.CurrentRansomAmount;

                var message = new NetworkEndPlayerCaptivityAttempted(heroId, partyId, playerParty.Position, data.Detail, facilitatorId, ransomAmount);
                network.SendAll(message);

                var playerCaptivity = Campaign.Current.PlayerCaptivity;

                playerCaptivity._captorParty = null;
                playerCaptivity.CountOfOffers = 0;
                playerCaptivity.CurrentRansomAmount = 0;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(EndPlayerCaptivityAttempted));
            }
        });
    }

    /// <summary>
    /// The server released this client's hero; leave the captivity menus and any settlement the
    /// captor dragged the player into.
    /// </summary>
    private void Handle_NetworkPlayerCaptivityEnded(MessagePayload<NetworkPlayerCaptivityEnded> payload)
    {
        if (ModInformation.IsServer) return;

        PlayerCaptivityLogger.Debug("Handle_NetworkPlayerCaptivityEnded (client): leaving captivity menus/encounter");

        GameThread.Run(() =>
        {
            if (PlayerEncounter.Current != null)
            {
                PlayerEncounter.LeaveSettlement();
            }

            if (PlayerEncounter.Current != null)
            {
                PlayerEncounter.Finish(true);
            }
            else if (Campaign.Current.CurrentMenuContext != null)
            {
                GameMenu.ExitToLast();
            }
        });
    }

    private void Handle_NetworkPlayerCaptivityReleasePositionSet(MessagePayload<NetworkPlayerCaptivityReleasePositionSet> payload)
    {
        if (ModInformation.IsServer) return;

        var playerPartyId = payload.What.PlayerPartyId;
        var releasePosition = payload.What.ReleasePosition;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(playerPartyId, out var playerParty))
                return;

            using (new AllowedThread())
            {
                playerParty.Position = releasePosition;
                playerParty.SetMoveModeHold();
                playerParty.ResetNavigationToHold();

                if (!playerParty.IsCurrentlyAtSea)
                {
                    playerParty.Party.UpdateVisibilityAndInspected(playerParty.Position);
                }

                playerParty.Party.SetVisualAsDirty();
            }
        }, context: $"set released player party position {playerPartyId}");
    }
}
