using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Entity;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using GameInterface.Services.Players;
using LiteNetLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Crime;

internal class CrimeRatingHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;
    private readonly ICrimeRatingService ratings;
    private readonly IControllerIdProvider controller;
    private readonly INetwork network;

    public CrimeRatingHandler(IMessageBroker broker, IPlayerManager players, IObjectManager objects, ICrimeRatingService ratings, IControllerIdProvider controller, INetwork network)
    {
        this.broker = broker;
        this.players = players;
        this.objects = objects;
        this.ratings = ratings;
        this.controller = controller;
        this.network = network;
        broker.Subscribe<RequestCrimeRatingChange>(Request);
        broker.Subscribe<RequestCrimePayment>(Pay);
        broker.Subscribe<NetworkCrimePaymentResult>(PaymentResult);
        broker.Subscribe<NetworkCrimeRatingChanged>(Receive);
        broker.Subscribe<NetworkCrimeRatingNotification>(Notify);
    }

    private void Request(MessagePayload<RequestCrimeRatingChange> payload)
    {
        if (!ModInformation.IsServer || payload.Who is not NetPeer peer || !(payload.What.Delta > 0f)) return;
        GameThread.RunSafe(() =>
        {
            if (!players.TryGetPlayer(peer, out var player)
                || !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)
                || !objects.TryGetObjectWithLogging<IFaction>(payload.What.FactionId, out var faction)) return;
            ratings.Apply(hero, faction, payload.What.Delta, payload.What.ShowNotification);
        });
    }

    private void Pay(MessagePayload<RequestCrimePayment> payload)
    {
        if (!ModInformation.IsServer || payload.Who is not NetPeer peer) return;
        GameThread.RunSafe(() =>
        {
            if (!players.TryGetPlayer(peer, out var player)
                || !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)
                || !objects.TryGetObjectWithLogging<Settlement>(payload.What.SettlementId, out var settlement)) return;
            var accepted = objects.TryGetObjectWithLogging<IFaction>(payload.What.FactionId, out var faction)
                && settlement.MapFaction == faction && ratings.Pay(hero, settlement, payload.What.Method);
            var leaveMenu = accepted && payload.What.Method != CrimeModel.PaymentMethod.Execution
                && hero.DeathMark != KillCharacterAction.KillCharacterActionDetail.Murdered && hero.IsAlive;
            network.Send(peer, new NetworkCrimePaymentResult(player.HeroId, payload.What.SettlementId, accepted, leaveMenu));
        });
    }

    private void PaymentResult(MessagePayload<NetworkCrimePaymentResult> payload)
    {
        if (!ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            var data = payload.What;
            var context = Campaign.Current?.CurrentMenuContext;
            if (!players.TryGetPlayer(controller.ControllerId, out var player) || player.HeroId != data.HeroId
                || !objects.TryGetObjectWithLogging<Settlement>(data.SettlementId, out var settlement)
                || Settlement.CurrentSettlement != settlement || !Hero.MainHero.IsAlive
                || (context?.GameMenu?.StringId != "town_inside_criminal"
                    && context?.GameMenu?.StringId != "town_discuss_criminal_surrender")) return;
            if (!data.Accepted)
                Campaign.Current.GameMenuManager.RefreshMenuOptionConditions(context);
            else if (data.LeaveMenu)
                GameMenu.SwitchToMenu(settlement.IsCastle ? "castle_outside" : "town_outside");
        });
    }

    private void Receive(MessagePayload<NetworkCrimeRatingChanged> payload)
    {
        if (!ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            var data = payload.What;
            if (!players.TryGetPlayer(data.ControllerId, out var player) || player.HeroId != data.HeroId
                || float.IsNaN(data.Rating) || float.IsInfinity(data.Rating)) return;
            player.CrimeRatings[data.FactionId] = data.Rating;
        });
    }

    private void Notify(MessagePayload<NetworkCrimeRatingNotification> payload)
    {
        if (!ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            var data = payload.What;
            if (data.ControllerId != controller.ControllerId
                || !players.TryGetPlayer(data.ControllerId, out var player) || player.HeroId != data.HeroId
                || data.Delta.ApproximatelyEqualsTo(0f)
                || !objects.TryGetObjectWithLogging<IFaction>(data.FactionId, out var faction)) return;
            var text = new TextObject("{=hwq0RMRN}Your criminal rating with {FACTION_NAME} has {?IS_INCREASED}increased{?}decreased{\\?} by {CHANGE} to {NEW_RATING}");
            text.SetTextVariable("CHANGE", MathF.Round(MathF.Abs(data.Delta)));
            text.SetTextVariable("IS_INCREASED", data.Delta > 0f ? 1 : 0);
            text.SetTextVariable("FACTION_NAME", faction.Name);
            text.SetTextVariable("NEW_RATING", MathF.Round(data.Rating));
            MBInformationManager.AddQuickInformation(text);
        });
    }

    public void Dispose()
    {
        broker.Unsubscribe<RequestCrimeRatingChange>(Request);
        broker.Unsubscribe<RequestCrimePayment>(Pay);
        broker.Unsubscribe<NetworkCrimePaymentResult>(PaymentResult);
        broker.Unsubscribe<NetworkCrimeRatingChanged>(Receive);
        broker.Unsubscribe<NetworkCrimeRatingNotification>(Notify);
    }
}
