using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
namespace GameInterface.Services.MobileParties.Handlers
{
    /// <summary>
    /// Handles all speed changes
    /// </summary>
    public class PartySpeedCalculateHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<PartySpeedCalculateHandler>();

        public PartySpeedCalculateHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<CalculateSpeed>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<CalculateSpeed>(Handle);
        }

        private void Handle(MessagePayload<CalculateSpeed> obj)
        {
            var payload = obj.What;
            Logger.Information("Speed calculated? ({speed})", payload.Speed);

            /*
            if (objectManager.TryGetObject<Clan>(payload.ClanId, out var playerClan) == false)
            {
                Logger.Error("Unable to find clan ({clanId})", payload.ClanId);
                return;
            }

            if (objectManager.TryGetObject<Hero>(payload.AdoptedHeroId, out var adoptedHero) == false)
            {
                Logger.Error("Unable to find adopted hero ({heroId})", payload.AdoptedHeroId);
                return;
            }

            if (objectManager.TryGetObject<Hero>(payload.PlayerHeroId, out var playerHero) == false)
            {
                Logger.Error("Unable to find player hero ({heroId})", payload.PlayerHeroId);
                return;
            }

            ClanAdoptHeroPatch.RunFixedAdoptHero(adoptedHero, playerClan, playerHero);
            */
        }
    }
}
