using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Configuration;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// [Server] Keeps AI parties from starting an engagement with a player whose battle just ended, while the player is
/// still in the post battle screens and for <see cref="ModOptions.PostBattleAiAmnestySeconds"/> after they leave.
/// </summary>
/// <remarks>
/// Vanilla gets this for free because the campaign is paused under those screens, while the co-op world keeps running.
/// The protection starts when the server finalizes the battle. The client's <see cref="NetworkConversationEnded"/>,
/// sent on every PlayerEncounter.Finish, moves it to the amnesty. That notice only ends the sender's own screen phase,
/// and a client that never sends it stays protected for <see cref="MaxScreenSeconds"/> at most. Time is real seconds
/// counted only while the campaign runs. Player attackers are never blocked. Game thread only.
/// </remarks>
internal sealed class PostBattleAiAmnesty : IHandler
{
    internal const float MaxScreenSeconds = 300f;

    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly Dictionary<MobileParty, Protection> protections = new Dictionary<MobileParty, Protection>();

    public PostBattleAiAmnesty(IMessageBroker messageBroker, IPlayerManager playerManager, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.objectManager = objectManager;

        messageBroker.Subscribe<NetworkConversationEnded>(Handle_NetworkConversationEnded);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkConversationEnded>(Handle_NetworkConversationEnded);
    }

    private void Handle_NetworkConversationEnded(MessagePayload<NetworkConversationEnded> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (payload.Who is not NetPeer peer || !playerManager.TryGetPlayer(peer, out var player)) return;
            if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party)) return;

            LeaveScreens(party);
        }, context: nameof(PostBattleAiAmnesty));
    }

    internal void Protect(MobileParty party) => protections[party] = new Protection(MaxScreenSeconds, inScreens: true);

    // Later notices for the same party find it already in the amnesty and change nothing.
    private void LeaveScreens(MobileParty party)
    {
        if (!protections.TryGetValue(party, out var protection) || !protection.InScreens) return;

        var amnestySeconds = ModConfigProvider.ModOptions.PostBattleAiAmnestySeconds;
        if (amnestySeconds <= 0)
        {
            protections.Remove(party);
            return;
        }

        protections[party] = new Protection(amnestySeconds, inScreens: false);
    }

    internal void Tick(float realDt)
    {
        if (protections.Count == 0 || Campaign.Current.CampaignDt <= 0f) return;

        foreach (var party in protections.Keys.ToList())
        {
            var protection = protections[party];
            var secondsLeft = protection.SecondsLeft - realDt;

            if (secondsLeft <= 0f)
                protections.Remove(party);
            else
                protections[party] = new Protection(secondsLeft, protection.InScreens);
        }
    }

    // A protected player who starts a new battle leaves it open to AI joining it as usual.
    internal bool IsProtectedFrom(MobileParty target, MobileParty attacker)
    {
        if (protections.Count == 0 || target?.MapEvent != null || attacker == null) return false;

        return protections.ContainsKey(target) && !attacker.IsPlayerParty();
    }

    private readonly struct Protection
    {
        public readonly float SecondsLeft;
        public readonly bool InScreens;

        public Protection(float secondsLeft, bool inScreens)
        {
            SecondsLeft = secondsLeft;
            InScreens = inScreens;
        }
    }
}
