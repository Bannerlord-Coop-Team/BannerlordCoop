using Common;
using Common.Messaging;
using Common.Network.Messages;
using Common.Util;
using GameInterface.Configuration;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// [Server] Tracks the players whose battle just ended and who are still in the loot, prisoner or other post battle
/// screens. The occupancy pause counts them as busy, and AI parties can't engage them while
/// <see cref="ModOptions.PostBattleAiAmnestySeconds"/> is above 0.
/// </summary>
/// <remarks>
/// The screens start when the server finalizes the battle and end with the client's <see cref="NetworkConversationEnded"/>,
/// sent on every PlayerEncounter.Finish, or when the player disconnects. The amnesty counts real seconds from leaving
/// the screens, paused time included. Game thread only.
/// </remarks>
internal sealed class PostBattleScreens : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    // Null while the party is in the screens, otherwise when it left them.
    private readonly Dictionary<MobileParty, DateTime?> parties = new Dictionary<MobileParty, DateTime?>();

    public PostBattleScreens(IMessageBroker messageBroker, IPlayerManager playerManager, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.objectManager = objectManager;

        messageBroker.Subscribe<NetworkConversationEnded>(Handle_NetworkConversationEnded);
        messageBroker.Subscribe<PlayerConnectionStateChanged>(Handle_PlayerConnectionStateChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkConversationEnded>(Handle_NetworkConversationEnded);
        messageBroker.Unsubscribe<PlayerConnectionStateChanged>(Handle_PlayerConnectionStateChanged);
    }

    // An offline player's party (a battle cleaned up on load) never gets the client's notice.
    internal void Enter(MobileParty party)
    {
        if (!playerManager.IsOwnerOfPartyDisconnected(party))
            parties[party] = null;
    }

    // A captured player's party is parked and only finishes its encounter when released.
    internal bool IsInScreens(MobileParty party) =>
        party.IsActive && parties.TryGetValue(party, out var leftAt) && leftAt == null;

    // Settlement targets have no mobile party. A protected player who starts a new battle leaves it open to AI joining.
    internal bool IsProtectedFrom(MobileParty target, MobileParty attacker)
    {
        var amnestySeconds = ModConfigProvider.ModOptions.PostBattleAiAmnestySeconds;
        if (amnestySeconds <= 0 || target == null || attacker == null || target.MapEvent != null) return false;
        if (attacker.IsPlayerParty() || !parties.TryGetValue(target, out var leftAt)) return false;
        if (leftAt == null || DateTime.UtcNow - leftAt.Value < TimeSpan.FromSeconds(amnestySeconds)) return true;

        parties.Remove(target);
        return false;
    }

    private void Handle_NetworkConversationEnded(MessagePayload<NetworkConversationEnded> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (payload.Who is not NetPeer peer || !playerManager.TryGetPlayer(peer, out var player)) return;
            if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party)) return;
            if (!parties.TryGetValue(party, out var leftAt) || leftAt != null) return;

            if (ModConfigProvider.ModOptions.PostBattleAiAmnestySeconds > 0)
                parties[party] = DateTime.UtcNow;
            else
                parties.Remove(party);

            messageBroker.Publish(this, new PartyOccupancyChanged(party));
        }, context: nameof(PostBattleScreens));
    }

    private void Handle_PlayerConnectionStateChanged(MessagePayload<PlayerConnectionStateChanged> payload)
    {
        GameThread.RunSafe(() =>
        {
            foreach (var party in parties.Keys.Where(playerManager.IsOwnerOfPartyDisconnected).ToList())
                parties.Remove(party);
        }, context: nameof(PostBattleScreens));
    }
}
