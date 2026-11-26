using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Hideouts;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents.Messages;
using Helpers;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.SiegeEvents.Handlers;

/// <summary>
/// [Client] The player's side of the keep (lord's hall) stage: the troop picker behind the encounter's Attack option,
/// the stage messages, and the keep mission request once the roster locked.
/// </summary>
internal sealed class SiegeKeepEntryHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeKeepEntryHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly INetworkConfig configuration;
    private readonly ITroopSelectionEntries selectionEntries;
    private readonly BattleStartCoordinator battleStarts;
    private readonly ConcurrentDictionary<string, PendingEntry> pendingEntries = new();

    public SiegeKeepEntryHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager,
        INetworkConfig configuration, ITroopSelectionEntries selectionEntries, BattleStartCoordinator battleStarts)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.configuration = configuration;
        this.selectionEntries = selectionEntries;
        this.battleStarts = battleStarts;
        messageBroker.Subscribe<NetworkSiegeKeepEntryReply>(Handle_EntryReply);
        messageBroker.Subscribe<NetworkSiegeKeepPhaseChanged>(Handle_PhaseChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkSiegeKeepEntryReply>(Handle_EntryReply);
        messageBroker.Unsubscribe<NetworkSiegeKeepPhaseChanged>(Handle_PhaseChanged);
    }

    /// <summary>Opens the troop picker for the keep, called from the encounter's Attack option.</summary>
    internal void OpenTroopSelection(MenuCallbackArgs args)
    {
        var battle = PlayerEncounter.Battle ?? MobileParty.MainParty?.MapEvent;
        if (battle == null || !objectManager.TryGetIdWithLogging(battle, out var mapEventId)) return;
        Logger.Information("Attack on the keep of {MapEventId}: asking the server for this player's place", mapEventId);
        if (!RequestBlocking(mapEventId, queryOnly: true, Array.Empty<HideoutTroopSelectionEntry>(), out var reply)) return;

        if (reply.IsLocked)
        {
            RequestKeepMission(mapEventId);
            return;
        }

        if (reply.EscortAllowance == 0)
        {
            SubmitSelection(mapEventId, selectionEntries.CreateHeroOnlyRoster(Hero.MainHero));
            return;
        }

        var maximum = reply.EscortAllowance + 1;
        // The player's own hero, which the picker keeps selected.
        var minimum = 1;
        var selected = MobilePartyHelper.GetStrongestAndPriorTroops(MobileParty.MainParty, maximum, includePlayer: true);
        args.MenuContext.OpenTroopSelection(MobileParty.MainParty.MemberRoster, selected,
            character => !character.IsPlayerCharacter,
            roster => SubmitSelection(mapEventId, roster), maximum, minimum);
        ShowMessage($"Choose up to {reply.EscortAllowance} troops for the keep. The assault starts in {reply.SecondsRemaining} seconds.");
    }

    private void SubmitSelection(string mapEventId, TroopRoster selected)
    {
        if (selected == null || !selectionEntries.TryCreateEntries(selected, out var troops)) return;
        if (!RequestBlocking(mapEventId, queryOnly: false, troops, out var reply)) return;
        if (!reply.IsLocked)
            ShowMessage("Your keep troops are ready. Waiting for the other players.");
    }

    private bool RequestBlocking(string mapEventId, bool queryOnly, HideoutTroopSelectionEntry[] troops,
        out NetworkSiegeKeepEntryReply reply)
    {
        reply = default;
        var requestId = Guid.NewGuid().ToString();
        var pending = new PendingEntry();
        pendingEntries[requestId] = pending;
        try
        {
            network.SendAll(new NetworkSiegeKeepEntryRequest(requestId, mapEventId, queryOnly, troops));
            if (!GameThread.WaitWhilePumping(() => pending.Completed.IsSet,
                    DateTime.UtcNow + configuration.ObjectCreationTimeout))
            {
                ShowMessage("The server has not answered the keep request. Try again.");
                return false;
            }
            reply = pending.Reply;
            if (!reply.Accepted) ShowMessage(reply.Reason ?? "Unable to join the keep battle.");
            return reply.Accepted;
        }
        finally
        {
            pendingEntries.TryRemove(requestId, out _);
        }
    }

    private void Handle_EntryReply(MessagePayload<NetworkSiegeKeepEntryReply> payload)
    {
        if (!pendingEntries.TryGetValue(payload.What.RequestId, out var pending)) return;
        pending.Reply = payload.What;
        pending.Completed.Set();
    }

    private void Handle_PhaseChanged(MessagePayload<NetworkSiegeKeepPhaseChanged> payload)
    {
        var message = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(message.MapEventId, out var mapEvent)) return;
            if (!ReferenceEquals(MobileParty.MainParty?.MapEvent, mapEvent)) return;

            Logger.Information("Keep stage {Phase} for {MapEventId}: seated={Seated} escorts={Escorts}",
                message.Phase, message.MapEventId, message.IsSeated, message.EscortAllowance);
            ApplyPhase(mapEvent, message);
        }, context: nameof(Handle_PhaseChanged));
    }

    private void ApplyPhase(MapEvent mapEvent, NetworkSiegeKeepPhaseChanged message)
    {
        switch (message.Phase)
        {
            case SiegeKeepPhase.Choosing:
                ResetLocalWallsResult(mapEvent);
                if (!message.IsSeated)
                {
                    ShowMessage("Walls taken. The defenders fell back to the keep. The keep has no place left for you, you will see the result.");
                    return;
                }
                ShowMessage($"Walls taken. The defenders fell back to the keep. Attack to choose up to {message.EscortAllowance} troops within {message.SecondsRemaining} seconds.");
                return;
            case SiegeKeepPhase.Locked:
                if (!message.IsSeated)
                {
                    ShowMessage("The keep battle has started. You will see the result.");
                    return;
                }
                ShowMessage("The keep assault is starting.");
                RequestKeepMission(message.MapEventId);
                return;
            case SiegeKeepPhase.Closed:
                ShowMessage("Nobody is left to lead the keep assault.");
                return;
        }
    }

    // The walls mission left its local pull-back (or a lagging victory) here; the server's event stays unresolved.
    private static void ResetLocalWallsResult(MapEvent mapEvent)
    {
        if (mapEvent.BattleState == BattleState.None) return;

        using (new AllowedThread())
        {
            mapEvent.ResetBattleState();
        }
    }

    // The server answers through the normal mission start, which opens the keep for every seated player.
    private void RequestKeepMission(string mapEventId)
    {
        if (!objectManager.TryGetIdWithLogging(MobileParty.MainParty, out var partyId) ||
            !battleStarts.RequestBlocking(BattleStartMode.Mission, mapEventId, partyId))
            ShowMessage("Unable to open the keep battle. Press Attack to try again.");
    }

    private static void ShowMessage(string text) => InformationManager.DisplayMessage(new InformationMessage(text));

    private sealed class PendingEntry
    {
        public ManualResetEventSlim Completed { get; } = new(false);
        public NetworkSiegeKeepEntryReply Reply;
    }
}
