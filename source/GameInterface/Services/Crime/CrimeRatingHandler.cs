using Common;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
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

    public CrimeRatingHandler(IMessageBroker broker, IPlayerManager players, IObjectManager objects, ICrimeRatingService ratings)
    {
        this.broker = broker;
        this.players = players;
        this.objects = objects;
        this.ratings = ratings;
        broker.Subscribe<RequestCrimeRatingChange>(Request);
        broker.Subscribe<NetworkCrimeRatingChanged>(Receive);
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

    public void Dispose()
    {
        broker.Unsubscribe<RequestCrimeRatingChange>(Request);
        broker.Unsubscribe<NetworkCrimeRatingChanged>(Receive);
    }
}
