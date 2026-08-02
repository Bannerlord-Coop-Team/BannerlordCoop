using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Heroes.Handlers
{
    /// <summary>
    /// Replicates server-authoritative hero relation changes to clients.
    /// </summary>
    public class CharacterRelationManagerHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public CharacterRelationManagerHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<HeroRelationChanged>(Handle);
            messageBroker.Subscribe<NetworkHeroRelationChanged>(Handle);
            messageBroker.Subscribe<ChangeRelationBetweenHeroes>(HandleChangeRelationBetweenHeroes);
            messageBroker.Subscribe<NetworkChangeRelationBetweenHeroes>(HandleNetworkChangeRelationBetweenHeroes);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<HeroRelationChanged>(Handle);
            messageBroker.Unsubscribe<NetworkHeroRelationChanged>(Handle);
            messageBroker.Unsubscribe<ChangeRelationBetweenHeroes>(HandleChangeRelationBetweenHeroes);
            messageBroker.Unsubscribe<NetworkChangeRelationBetweenHeroes>(HandleNetworkChangeRelationBetweenHeroes);
        }

        private void Handle(MessagePayload<HeroRelationChanged> obj)
        {
            if (ModInformation.IsClient) return;

            var payload = obj.What;
            network.SendAll(new NetworkHeroRelationChanged(payload.Hero1Id, payload.Hero2Id, payload.Value));
        }

        private void Handle(MessagePayload<NetworkHeroRelationChanged> obj)
        {
            var payload = obj.What;

            GameThread.RunSafe(() =>
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(payload.Hero1Id, out var hero1)) return;
                if (!objectManager.TryGetObjectWithLogging<Hero>(payload.Hero2Id, out var hero2)) return;

                // The change is applied as an absolute value (SetHeroRelation) to match the server exactly
                // and avoid the RNG/model factor ChangeRelationAction applies to positive gains; capture the
                // delta first so we can still show the bottom-left popup vanilla would.
                int delta = payload.Value - CharacterRelationManager.GetHeroRelation(hero1, hero2);

                using (new AllowedThread())
                {
                    CharacterRelationManager.SetHeroRelation(hero1, hero2, payload.Value);
                }
            }, context: $"apply hero relation {payload.Hero1Id}<->{payload.Hero2Id}");
        }
        private void HandleChangeRelationBetweenHeroes(MessagePayload<ChangeRelationBetweenHeroes> payload)
        {
            if (!objectManager.TryGetIdWithLogging(payload.What.Hero1, out var hero1)) return;
            if (!objectManager.TryGetIdWithLogging(payload.What.Hero2, out var hero2)) return;

            network.SendAll(new NetworkChangeRelationBetweenHeroes(hero1, hero2, payload.What.Relation));
        }
        private void HandleNetworkChangeRelationBetweenHeroes(MessagePayload<NetworkChangeRelationBetweenHeroes> obj)
        {
            var payload = obj.What;
            GameThread.RunSafe(() =>
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(payload.Hero1Id, out var hero1)) return;
                if (!objectManager.TryGetObjectWithLogging<Hero>(payload.Hero2Id, out var hero2)) return;

                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(hero1, hero2, payload.Relation, true);
            });
        }
    }
}
