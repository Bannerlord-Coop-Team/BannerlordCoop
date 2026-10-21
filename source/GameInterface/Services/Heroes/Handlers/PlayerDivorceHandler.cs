using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Heroes.RomanceFlow;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Heroes.Handlers;

/// <summary>Routes confirmed divorce requests to the server's authoritative divorce rules.</summary>
internal class PlayerDivorceHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IPlayerDivorceRules rules;

    public PlayerDivorceHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager,
        IPlayerManager playerManager, IPlayerDivorceRules rules)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.rules = rules;
        messageBroker.Subscribe<PlayerDivorceRequested>(Handle_PlayerDivorceRequested);
        messageBroker.Subscribe<RequestPlayerDivorce>(Handle_RequestPlayerDivorce);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerDivorceRequested>(Handle_PlayerDivorceRequested);
        messageBroker.Unsubscribe<RequestPlayerDivorce>(Handle_RequestPlayerDivorce);
    }

    private void Handle_PlayerDivorceRequested(MessagePayload<PlayerDivorceRequested> payload)
    {
        if (ModInformation.IsServer) return;
        var request = payload.What;
        if (!objectManager.TryGetIdWithLogging(request.PlayerHero, out var heroId) ||
            !objectManager.TryGetIdWithLogging(request.Spouse, out var spouseId)) return;

        network.SendAll(new RequestPlayerDivorce(heroId, spouseId));
    }

    private void Handle_RequestPlayerDivorce(MessagePayload<RequestPlayerDivorce> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            var request = payload.What;
            if (!playerManager.IsSenderControllingHero(payload.Who, request.PlayerHeroId)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(request.PlayerHeroId, out var playerHero) ||
                !objectManager.TryGetObjectWithLogging<Hero>(request.SpouseId, out var spouse) ||
                !rules.TryApply(playerHero, spouse, out var clansSeparated, out _))
            {
                if (payload.Who is NetPeer peer)
                    network.Send(peer, new SendInformationMessage(GameTexts.FindText("str_coop_divorce_unavailable").ToString()));
                return;
            }

            var text = GameTexts.FindText("str_coop_divorce_completed")
                .SetTextVariable("FIRST", playerHero.Name).SetTextVariable("SECOND", spouse.Name).ToString();
            if (!clansSeparated)
                text += " " + GameTexts.FindText("str_coop_divorce_clan_unchanged");
            Notify(playerHero, text);
            Notify(spouse, text);
        }, context: nameof(Handle_RequestPlayerDivorce));
    }

    private void Notify(Hero hero, string text)
    {
        if (playerManager.TryGetPlayer(hero, out var player) && playerManager.TryGetPeer(player.ControllerId, out var peer))
            network.Send(peer, new SendInformationMessage(text));
    }
}
