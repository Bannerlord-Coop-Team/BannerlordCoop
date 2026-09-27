using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.BugReporting;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MobileParties.Messages.Unstuck;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Settlements.Interfaces;
using GameInterface.Services.SiegeEvents.Interfaces;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Dedicated recovery flow behind coop.unstuck. The client forwards
/// <see cref="PlayerUnstuckRequested"/> to the server as <see cref="NetworkRequestPlayerUnstuck"/>;
/// the server force-applies every applicable exit (captivity, map event, follower army, siege camp,
/// settlement) for the requesting connection's registered player, with each step guarded
/// independently, so one broken exit flow cannot block the others; the
/// <see cref="NetworkPlayerUnstuckResult"/> reply then lets the requesting client clear the
/// local-only encounter and menu state the server cannot see. Intentionally separate from the
/// normal exit request flows — those carry gating state that may be exactly what is stuck.
/// </summary>
internal class PlayerUnstuckHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerUnstuckHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IBugReportService bugReportService;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ISettlementInterface settlementInterface;
    private readonly ISiegeEventInterface siegeEventInterface;

    public PlayerUnstuckHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IBugReportService bugReportService,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ISettlementInterface settlementInterface,
        ISiegeEventInterface siegeEventInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.bugReportService = bugReportService;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.settlementInterface = settlementInterface;
        this.siegeEventInterface = siegeEventInterface;

        messageBroker.Subscribe<PlayerUnstuckRequested>(Handle_PlayerUnstuckRequested);
        messageBroker.Subscribe<NetworkRequestPlayerUnstuck>(Handle_NetworkRequestPlayerUnstuck);
        messageBroker.Subscribe<NetworkPlayerUnstuckResult>(Handle_NetworkPlayerUnstuckResult);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerUnstuckRequested>(Handle_PlayerUnstuckRequested);
        messageBroker.Unsubscribe<NetworkRequestPlayerUnstuck>(Handle_NetworkRequestPlayerUnstuck);
        messageBroker.Unsubscribe<NetworkPlayerUnstuckResult>(Handle_NetworkPlayerUnstuckResult);
    }

    /// <summary>
    /// Client: forward the local unstuck request to the server.
    /// </summary>
    private void Handle_PlayerUnstuckRequested(MessagePayload<PlayerUnstuckRequested> payload)
    {
        if (ModInformation.IsServer) return;

        if (!objectManager.TryGetIdWithLogging(payload.What.Party, out var partyId)) return;

        // Optional: the hero speeds up server-side captivity resolution but is not required.
        objectManager.TryGetId(Hero.MainHero, out var heroId);

        network.SendAll(new NetworkRequestPlayerUnstuck(partyId, heroId));
    }

    /// <summary>
    /// Server: force-apply every applicable exit for the requesting connection's registered party
    /// and report back to that connection.
    /// </summary>
    private void Handle_NetworkRequestPlayerUnstuck(MessagePayload<NetworkRequestPlayerUnstuck> payload)
    {
        if (!ModInformation.IsServer) return;

        if (!(payload.Who is NetPeer requester))
        {
            Logger.Warning("{Message} arrived without a source peer; cannot resolve the requesting player",
                nameof(NetworkRequestPlayerUnstuck));
            return;
        }

        var data = payload.What;
        GameThread.RunSafe(() => UnstuckRequester(requester, data), context: nameof(PlayerUnstuckHandler));
    }

    private void UnstuckRequester(NetPeer requester, NetworkRequestPlayerUnstuck request)
    {
        if (!playerManager.TryGetPlayer(requester, out var player))
        {
            // No kick: a peer without a player may still be joining.
            Logger.Warning("Unstuck request from peer {PeerId} with no registered player", requester.Id);
            return;
        }

        // The request ids can be stale local state; only the registration decides what is changed.
        if (request.PartyId != player.MobilePartyId ||
            (!string.IsNullOrEmpty(request.HeroId) && request.HeroId != player.HeroId))
        {
            Logger.Warning(
                "Unstuck request names party {RequestedPartyId} and hero {RequestedHeroId} but controller {ControllerId} is registered with party {PartyId} and hero {HeroId}; unsticking the registered player",
                request.PartyId, request.HeroId, player.ControllerId, player.MobilePartyId, player.HeroId);
        }

        try
        {
            ApplyServerUnstuck(player, requester);
        }
        finally
        {
            TryRequestBugReport(requester);
        }
    }

    private void TryRequestBugReport(NetPeer requester)
    {
        if (!BugReportConfig.UnstuckCommandReportsEnabled) return;

        try
        {
            bugReportService.RequestReport("unstuck", requester);
        }
        catch (Exception exception)
        {
            Logger.Warning(exception, "Could not start the unstuck diagnostic bug report");
        }
    }

    // The DEBUG siege defense fixture patches this method by name and binds its player parameter.
    private void ApplyServerUnstuck(Player player, NetPeer requester)
    {
        var partyId = player.MobilePartyId;
        var actions = new List<string>();

        if (!objectManager.TryGetObjectWithLogging(partyId, out MobileParty party))
        {
            actions.Add($"Party '{partyId}' was not found on the server; nothing was applied.");
            network.Send(requester, new NetworkPlayerUnstuckResult(partyId, actions.ToArray()));
            return;
        }

        var hero = ResolvePlayerHero(player, party);

        // Every step runs with patches live (no AllowedThread) so the applied changes replicate to
        // clients through their normal sync flows, and each step is guarded independently.
        if (hero != null && hero.IsPrisoner)
        {
            TryStep(actions, "captivity release", () =>
            {
                // Patched: a registered player hero routes through the coop release flow, which also
                // restores the deactivated player party.
                EndCaptivityAction.ApplyByEscape(hero);
                return $"Applied captivity release (escape) for hero {hero.StringId}.";
            });
        }

        var army = party.Army;
        if (army?.LeaderParty == party)
        {
            actions.Add("Preserved the player-led army and its attached parties.");
        }
        else if (army != null)
        {
            TryStep(actions, "army removal", () =>
            {
                // Same pair the army-removal network flow uses: the publish makes ArmyHandler
                // broadcast NetworkRemovePartyInArmy to clients, the port applies it here.
                messageBroker.Publish(party, new MobilePartyInArmyRemoved(army, party, null));
                ArmyPatches.RemoveMobilePartyInArmy(party, army, null);
                return $"Removed the party from the army of {army.LeaderParty?.StringId ?? "<unknown leader>"}.";
            });
        }

        if (party.BesiegerCamp != null)
        {
            TryStep(actions, "siege camp removal", () =>
            {
                // Same application the break-siege request handler uses on the server.
                siegeEventInterface.BreakSiege(party);
                return "Removed the party from its siege camp.";
            });
        }

        var settlement = party.CurrentSettlement;
        if (settlement != null)
        {
            TryStep(actions, "settlement exit", () =>
            {
                // Patches live: the leave prefix publishes the attempt, which the settlement exit
                // handler broadcasts to clients while the native leave applies here.
                settlementInterface.PartyLeaveSettlement(party);
                return $"Removed the party from settlement {settlement.StringId}.";
            });
        }

        if (party.Party?.MapEvent != null)
        {
            TryStep(actions, "map event removal", () =>
            {
                // The party-side setter also releases attached members on the server and clients.
                messageBroker.Publish(party, new PlayerLeaveBattleAttempted(party.Party));
                if (party.Party.MapEvent != null)
                    throw new InvalidOperationException("The battle leave flow did not remove the party from its map event.");

                return "Removed the party from its map event.";
            });
        }

        if (actions.Count == 0)
        {
            actions.Add("No server-side stuck state found (captivity, map event, army, siege camp, settlement).");
        }

        network.Send(requester, new NetworkPlayerUnstuckResult(partyId, actions.ToArray()));
    }

    /// <summary>
    /// The registered player's hero, else the party leader. A captured player's party can have no
    /// leader, so the registration comes first.
    /// </summary>
    private Hero ResolvePlayerHero(Player player, MobileParty party)
    {
        if (!string.IsNullOrEmpty(player.HeroId) && objectManager.TryGetObject(player.HeroId, out Hero playerHero)) return playerHero;

        return party.LeaderHero;
    }

    /// <summary>
    /// Requesting client: the server finished its part — clear the local-only encounter and menu
    /// state it cannot see, then surface the combined report.
    /// </summary>
    private void Handle_NetworkPlayerUnstuckResult(MessagePayload<NetworkPlayerUnstuckResult> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        var serverActions = data.Actions ?? Array.Empty<string>();

        GameThread.RunSafe(() => ApplyLocalCleanup(data.PartyId, serverActions), context: nameof(PlayerUnstuckHandler));
    }

    private void ApplyLocalCleanup(string partyId, string[] serverActions)
    {
        var mainParty = MobileParty.MainParty;
        if (mainParty == null) return;

        // Only the stuck player's client applies local cleanup.
        if (!objectManager.TryGetId(mainParty, out var mainPartyId) || mainPartyId != partyId) return;

        var actions = new List<string>(serverActions);

        if (Hero.MainHero?.IsPrisoner == true)
        {
            // The captivity release flow owns the menus and the party restore; racing it here would
            // fight the release that the server just started.
            actions.Add("Awaiting the server captivity release; local state is restored by the release flow.");
        }
        else if (PlayerEncounter.Current != null || mainParty.CurrentSettlement != null || GetLocalEncounterSettlementSafe() != null)
        {
            TryStep(actions, "local encounter cleanup", () =>
            {
                using (new AllowedThread())
                {
                    settlementInterface.EndSettlementEncounter();
                }
                return "Cleared the local settlement/encounter state.";
            });
        }
        else if (GetCurrentMenuContextSafe() != null)
        {
            TryStep(actions, "menu exit", () =>
            {
                GameMenu.ExitToLast();
                return "Exited the stuck game menu.";
            });
        }

        messageBroker.Publish(this, new PlayerUnstuckCompleted(partyId, actions.ToArray()));
        ShowReport(actions);
    }

    private static void TryStep(List<string> actions, string step, Func<string> apply)
    {
        try
        {
            actions.Add(apply());
        }
        catch (Exception e)
        {
            Logger.Error(e, "Unstuck step {Step} failed", step);
            actions.Add($"Failed {step}: {e.GetType().Name} ({e.Message}).");
        }
    }

    /// <summary>
    /// Settlement.CurrentSettlement dereferences the local captivity and encounter statics, which
    /// can be half-built in exactly the broken states this flow recovers from. Treat a throw as none.
    /// </summary>
    private static Settlement GetLocalEncounterSettlementSafe()
    {
        try
        {
            return Settlement.CurrentSettlement;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private static MenuContext GetCurrentMenuContextSafe()
    {
        try
        {
            return Campaign.Current.CurrentMenuContext;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private static void ShowReport(List<string> actions)
    {
        try
        {
            foreach (var action in actions)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[Unstuck] {action}"));
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to display the unstuck report");
        }
    }
}
