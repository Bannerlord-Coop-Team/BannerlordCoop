using Common;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Entity;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using GameInterface.Services.Players;
using LiteNetLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Crime;

internal class CrimeRatingHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;
    private readonly ICrimeRatingService ratings;
    private readonly IControllerIdProvider controller;

    public CrimeRatingHandler(IMessageBroker broker, IPlayerManager players, IObjectManager objects, ICrimeRatingService ratings, IControllerIdProvider controller)
    {
        this.broker = broker;
        this.players = players;
        this.objects = objects;
        this.ratings = ratings;
        this.controller = controller;
        broker.Subscribe<RequestCrimeRatingChange>(Request);
        broker.Subscribe<NetworkCrimeRatingChanged>(Receive);
        broker.Subscribe<NetworkCrimeRatingNotification>(Notify);
    }

    private void Request(MessagePayload<RequestCrimeRatingChange> payload)
    {
        if (!ModInformation.IsServer || payload.Who is not NetPeer peer) return;
        GameThread.RunSafe(() =>
        {
            if (!players.TryGetPlayer(peer, out var player)
                || !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)
                || !objects.TryGetObjectWithLogging<IFaction>(payload.What.FactionId, out var faction)) return;
            ratings.Apply(hero, faction, payload.What.Delta, payload.What.ShowNotification);
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
        broker.Unsubscribe<NetworkCrimeRatingChanged>(Receive);
        broker.Unsubscribe<NetworkCrimeRatingNotification>(Notify);
    }
}
