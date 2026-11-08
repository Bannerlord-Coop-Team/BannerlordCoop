using Common;
using Common.Messaging;
using Common.Network.Messages;
using Common.Util;
using GameInterface.Configuration;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// [Server] Tracks the players whose battle just ended and who are still in the loot, prisoner or other post battle
/// screens. The occupancy pause counts them as busy, and AI parties can't engage them while
/// <see cref="ModOptions.PostBattleAiAmnestySeconds"/> is above 0.
/// </summary>
/// <remarks>
/// The screens start when the server finalizes the battle and end with the client's <see cref="NetworkConversationEnded"/>,
/// sent on every PlayerEncounter.Finish, a capture, a disconnect or after <see cref="MaxScreenSeconds"/>. Times are
/// real seconds, paused time included. Game thread only.
/// </remarks>
internal sealed class PostBattleScreens : IHandler
{
    // Backstop for a client that never sends its notice. Checked when the screens are read, not on a tick.
    internal const double MaxScreenSeconds = 180;

    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly Dictionary<MobileParty, Entry> parties = new Dictionary<MobileParty, Entry>();
    // A client can finish its encounter before the server finalizes that battle.
    private readonly Dictionary<MobileParty, MapEvent> finishedBeforeFinalize = new Dictionary<MobileParty, MapEvent>();

    internal Func<DateTime> UtcNow = () => DateTime.UtcNow;

    public PostBattleScreens(IMessageBroker messageBroker, IPlayerManager playerManager, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.objectManager = objectManager;

        messageBroker.Subscribe<NetworkConversationEnded>(Handle_NetworkConversationEnded);
        messageBroker.Subscribe<PlayerConnectionStateChanged>(Handle_PlayerConnectionStateChanged);
        messageBroker.Subscribe<PrisonerTaken>(Handle_PrisonerTaken);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkConversationEnded>(Handle_NetworkConversationEnded);
        messageBroker.Unsubscribe<PlayerConnectionStateChanged>(Handle_PlayerConnectionStateChanged);
        messageBroker.Unsubscribe<PrisonerTaken>(Handle_PrisonerTaken);
    }

    // A captured party is parked and doesn't finish its encounter until released. An offline player's party (a
    // battle cleaned up on load) never sends the notice either.
    internal void Enter(MobileParty party, MapEvent mapEvent)
    {
        if (finishedBeforeFinalize.TryGetValue(party, out var finishedMapEvent))
        {
            finishedBeforeFinalize.Remove(party);
            if (finishedMapEvent == mapEvent) return;
        }

        if (!party.IsActive || playerManager.IsOwnerOfPartyDisconnected(party)) return;

        parties[party] = new Entry(UtcNow(), leftAt: null);
    }

    internal bool IsInScreens(MobileParty party)
    {
        if (!parties.TryGetValue(party, out var entry) || entry.LeftAt != null) return false;
        if (UtcNow() - entry.EnteredAt < TimeSpan.FromSeconds(MaxScreenSeconds)) return true;

        parties.Remove(party);
        return false;
    }

    // Settlement targets have no mobile party. A protected player who starts a new battle leaves it open to AI joining.
    internal bool IsProtectedFrom(MobileParty target, MobileParty attacker)
    {
        var amnestySeconds = ModConfigProvider.ModOptions.PostBattleAiAmnestySeconds;
        if (amnestySeconds <= 0 || target == null || attacker == null || target.MapEvent != null) return false;
        if (attacker.IsPlayerParty() || !parties.TryGetValue(target, out var entry)) return false;

        var protectedUntil = entry.LeftAt?.AddSeconds(amnestySeconds) ?? entry.EnteredAt.AddSeconds(MaxScreenSeconds);
        if (UtcNow() < protectedUntil) return true;

        parties.Remove(target);
        return false;
    }

    private void Handle_NetworkConversationEnded(MessagePayload<NetworkConversationEnded> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (payload.Who is not NetPeer peer || !playerManager.TryGetPlayer(peer, out var player)) return;
            if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party)) return;

            if (!parties.TryGetValue(party, out var entry) || entry.LeftAt != null)
            {
                if (party.MapEvent != null)
                    finishedBeforeFinalize[party] = party.MapEvent;
                return;
            }

            if (ModConfigProvider.ModOptions.PostBattleAiAmnestySeconds > 0)
                parties[party] = new Entry(entry.EnteredAt, UtcNow());
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
        public readonly DateTime EnteredAt;
        // Null while the party is in the screens.
        public readonly DateTime? LeftAt;

        public Entry(DateTime enteredAt, DateTime? leftAt)
        {
            EnteredAt = enteredAt;
            LeftAt = leftAt;
        }
    }
}
