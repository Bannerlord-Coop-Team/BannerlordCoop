using Common;
using Common.Messaging;
using Common.Network.Messages;
using Common.Util;
using GameInterface.Configuration;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// [Server] Tracks the players whose battle just ended and who are still in the loot, prisoner or other post battle
/// screens. The occupancy pause counts them as busy, and AI parties can't engage them while
/// <see cref="ModOptions.PostBattleAiAmnestySeconds"/> is above 0.
/// </summary>
/// <remarks>
/// The screens start when the server finalizes the battle and end with the client's encounter finish
/// (<see cref="NetworkConversationEnded.FinishedMapEventId"/>), a capture, a disconnect or after
/// <see cref="MaxScreenSeconds"/>. Times are real seconds, paused time included. Game thread only.
/// </remarks>
internal sealed class PostBattleScreens : IHandler
{
    // Backstop for a client that never finishes its encounter.
    internal const double MaxScreenSeconds = 180;

    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly Dictionary<MobileParty, Entry> parties = new Dictionary<MobileParty, Entry>();
    // A client can finish its encounter before the server finalizes that battle. In a PvE battle the client's
    // MapEventRegistry finishes at once when the finalize lands after it left the mission, so that path keeps the old behaviour.
    private readonly Dictionary<MobileParty, (MapEvent MapEvent, DateTime FinishedAt)> finishedBeforeFinalize =
        new Dictionary<MobileParty, (MapEvent, DateTime)>();

    private bool disposed;

    internal Func<DateTime> UtcNow = () => DateTime.UtcNow;
    internal Action<TimeSpan, Action> Schedule = (delay, action) =>
        Task.Delay(delay).ContinueWith(_ => GameThread.RunSafe(action, context: nameof(PostBattleScreens)));

    public PostBattleScreens(IMessageBroker messageBroker, IPlayerManager playerManager, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.objectManager = objectManager;

        messageBroker.Subscribe<NetworkConversationEnded>(Handle_NetworkConversationEnded);
        messageBroker.Subscribe<PlayerConnectionStateChanged>(Handle_PlayerConnectionStateChanged);
        messageBroker.Subscribe<PrisonerTaken>(Handle_PrisonerTaken);
        messageBroker.Subscribe<MapEventFinalized>(Handle_MapEventFinalized);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkConversationEnded>(Handle_NetworkConversationEnded);
        messageBroker.Unsubscribe<PlayerConnectionStateChanged>(Handle_PlayerConnectionStateChanged);
        messageBroker.Unsubscribe<PrisonerTaken>(Handle_PrisonerTaken);
        messageBroker.Unsubscribe<MapEventFinalized>(Handle_MapEventFinalized);

        disposed = true;
        parties.Clear();
        finishedBeforeFinalize.Clear();
    }

    // A captured party is parked and doesn't finish its encounter until released. An offline player's party (a
    // battle cleaned up on load) never finishes it either.
    internal void Enter(MobileParty party, MapEvent mapEvent)
    {
        if (finishedBeforeFinalize.TryGetValue(party, out var finish))
        {
            finishedBeforeFinalize.Remove(party);
            if (finish.MapEvent == mapEvent)
            {
                if (ModConfigProvider.ModOptions.PostBattleAiAmnestySeconds > 0)
                    parties[party] = new Entry(null, finish.FinishedAt, finish.FinishedAt);
                return;
            }
        }

        if (!party.IsActive || playerManager.IsOwnerOfPartyDisconnected(party)) return;

        objectManager.TryGetId(mapEvent, out string mapEventId);
        var enteredAt = UtcNow();
        parties[party] = new Entry(mapEventId, enteredAt, leftAt: null);
        Schedule(TimeSpan.FromSeconds(MaxScreenSeconds), () => Expire(party, enteredAt));
    }

    internal bool IsInScreens(MobileParty party) => parties.TryGetValue(party, out var entry) && entry.LeftAt == null;

    // Settlement targets have no mobile party. A protected player who starts a new battle leaves it open to AI joining.
    internal bool IsProtectedFrom(MobileParty target, MobileParty attacker)
    {
        var amnestySeconds = ModConfigProvider.ModOptions.PostBattleAiAmnestySeconds;
        if (amnestySeconds <= 0 || target == null || attacker == null || target.MapEvent != null) return false;
        if (attacker.IsPlayerParty() || !parties.TryGetValue(target, out var entry)) return false;
        if (entry.LeftAt == null || UtcNow() < entry.LeftAt.Value.AddSeconds(amnestySeconds)) return true;

        parties.Remove(target);
        return false;
    }

    // Only for the screens this call started, not a later battle's.
    private void Expire(MobileParty party, DateTime enteredAt)
    {
        if (disposed) return;
        if (!parties.TryGetValue(party, out var entry) || entry.LeftAt != null || entry.EnteredAt != enteredAt) return;

        parties.Remove(party);
        messageBroker.Publish(this, new PartyOccupancyChanged(party));
    }

    private void Handle_NetworkConversationEnded(MessagePayload<NetworkConversationEnded> payload)
    {
        // Failed or stale conversation approvals send no map event. A client older than this change never sends one
        // either, so its screens hold until the cap (Coop requires matching versions).
        var finishedMapEventId = payload.What.FinishedMapEventId;
        if (string.IsNullOrEmpty(finishedMapEventId)) return;

        GameThread.RunSafe(() =>
        {
            if (disposed) return;
            if (payload.Who is not NetPeer peer || !playerManager.TryGetPlayer(peer, out var player)) return;
            if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party)) return;

            // Only the battle whose encounter finished, not one the party is in now. An old encounter can finish as a
            // new battle starts.
            if (!parties.TryGetValue(party, out var entry) || entry.LeftAt != null || entry.MapEventId != finishedMapEventId)
            {
                if (party.MapEvent != null && objectManager.TryGetId(party.MapEvent, out string currentMapEventId) &&
                    currentMapEventId == finishedMapEventId)
                    finishedBeforeFinalize[party] = (party.MapEvent, UtcNow());
                return;
            }

            if (ModConfigProvider.ModOptions.PostBattleAiAmnestySeconds > 0)
                parties[party] = new Entry(entry.MapEventId, entry.EnteredAt, UtcNow());
            else
                parties.Remove(party);

            messageBroker.Publish(this, new PartyOccupancyChanged(party));
        }, context: nameof(PostBattleScreens));
    }

    private void Handle_PrisonerTaken(MessagePayload<PrisonerTaken> payload)
    {
        var party = payload.What.PrisonerParty;
        if (party == null) return;

        GameThread.RunSafe(() =>
        {
            parties.Remove(party);
            finishedBeforeFinalize.Remove(party);
        }, context: nameof(PostBattleScreens));
    }

    // Finishes kept for a battle that finalized without the party in it.
    private void Handle_MapEventFinalized(MessagePayload<MapEventFinalized> payload)
    {
        var mapEvent = payload.What.MapEvent;
        foreach (var party in finishedBeforeFinalize.Where(pair => pair.Value.MapEvent == mapEvent).Select(pair => pair.Key).ToList())
            finishedBeforeFinalize.Remove(party);
    }

    private void Handle_PlayerConnectionStateChanged(MessagePayload<PlayerConnectionStateChanged> payload)
    {
        GameThread.RunSafe(() =>
        {
            foreach (var party in parties.Keys.Where(playerManager.IsOwnerOfPartyDisconnected).ToList())
                parties.Remove(party);

            foreach (var party in finishedBeforeFinalize.Keys.Where(playerManager.IsOwnerOfPartyDisconnected).ToList())
                finishedBeforeFinalize.Remove(party);
        }, context: nameof(PostBattleScreens));
    }

    private readonly struct Entry
    {
        public readonly string MapEventId;
        public readonly DateTime EnteredAt;
        // Null while the party is in the screens.
        public readonly DateTime? LeftAt;

        public Entry(string mapEventId, DateTime enteredAt, DateTime? leftAt)
        {
            MapEventId = mapEventId;
            EnteredAt = enteredAt;
            LeftAt = leftAt;
        }
    }
}
