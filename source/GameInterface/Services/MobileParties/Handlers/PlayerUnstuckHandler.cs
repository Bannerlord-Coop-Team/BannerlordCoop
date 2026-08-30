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
using GameInterface.Services.Settlements.Interfaces;
using GameInterface.Services.SiegeEvents.Interfaces;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
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
/// Dedicated recovery flow behind coop.debug.mobileparty.unstuck. The client forwards
/// <see cref="PlayerUnstuckRequested"/> to the server as <see cref="NetworkRequestPlayerUnstuck"/>;
/// the server force-applies every applicable exit (captivity, map event, army, siege camp,
/// settlement) with each step guarded independently, so one broken exit flow cannot block the
/// others; the
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
    /// Server: force-apply every applicable exit for the requesting party and report back.
    /// </summary>
    private void Handle_NetworkRequestPlayerUnstuck(MessagePayload<NetworkRequestPlayerUnstuck> payload)
    {
        if (!ModInformation.IsServer) return;

        var data = payload.What;
        var requester = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            try
            {
                ApplyServerUnstuck(data.PartyId, data.HeroId);
            }
            finally
            {
                TryRequestBugReport(data.PartyId, requester);
            }
        }, context: nameof(PlayerUnstuckHandler));
    }

    private void TryRequestBugReport(string partyId, NetPeer requester)
    {
        if (!BugReportConfig.UnstuckCommandReportsEnabled) return;

        if (requester == null ||
            !playerManager.TryGetPlayer(requester, out var player) ||
            player.MobilePartyId != partyId)
        {
            return;
        }

        try
        {
            bugReportService.RequestReport("unstuck", requester);
        }
        catch (Exception exception)
        {
            Logger.Warning(exception, "Could not start the unstuck diagnostic bug report");
        }
    }

    private void ApplyServerUnstuck(string partyId, string heroId)
    {
        var actions = new List<string>();

        if (!objectManager.TryGetObjectWithLogging(partyId, out MobileParty party))
        {
            actions.Add($"Party '{partyId}' was not found on the server; nothing was applied.");
            network.SendAll(new NetworkPlayerUnstuckResult(partyId, actions.ToArray()));
            return;
        }

        var hero = ResolvePlayerHero(partyId, heroId, party);

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
        if (army != null)
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
                // Reuse the normal authoritative leave flow so the removal and client cleanup are broadcast.
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

        network.SendAll(new NetworkPlayerUnstuckResult(partyId, actions.ToArray()));
    }

    /// <summary>
    /// The client's hero when it sent one, else the party leader, else the registered player owning
    /// the party. A captured player's party can have no leader, so the fallbacks matter.
    /// </summary>
    private Hero ResolvePlayerHero(string partyId, string heroId, MobileParty party)
    {
        if (!string.IsNullOrEmpty(heroId) && objectManager.TryGetObject(heroId, out Hero requestedHero)) return requestedHero;

        if (party.LeaderHero != null) return party.LeaderHero;

        var player = playerManager.Players.FirstOrDefault(candidate => candidate.MobilePartyId == partyId);
        if (player != null && objectManager.TryGetObject(player.HeroId, out Hero playerHero)) return playerHero;

        return null;
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

        // The result is broadcast; only the stuck player's client applies local cleanup.
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
