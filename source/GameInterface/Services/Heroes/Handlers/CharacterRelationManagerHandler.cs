using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

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
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<HeroRelationChanged>(Handle);
            messageBroker.Unsubscribe<NetworkHeroRelationChanged>(Handle);
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

                NotifyLocalPlayer(hero1, hero2, delta, payload.Value);
            }, context: $"apply hero relation {payload.Hero1Id}<->{payload.Hero2Id}");
        }

        // Show the relation-change quick info on the client whose own hero is involved (the sync applies
        // the value silently via SetHeroRelation, so vanilla's OnHeroRelationChanged popup never fires).
        private static void NotifyLocalPlayer(Hero hero1, Hero hero2, int delta, int newValue)
        {
            if (delta == 0) return;

            var mainHero = Hero.MainHero;
            if (mainHero == null || (hero1 != mainHero && hero2 != mainHero)) return;

            var other = hero1 == mainHero ? hero2 : hero1;
            var text = new TextObject("{=coop_relation_changed}Relation with {HERO}: {AMOUNT} (now {TOTAL})");
            text.SetTextVariable("HERO", other.Name);
            text.SetTextVariable("AMOUNT", (delta > 0 ? "+" : "") + delta);
            text.SetTextVariable("TOTAL", newValue);
            MBInformationManager.AddQuickInformation(text, 0, other.CharacterObject, null, "event:/ui/notification/relation");
        }
    }
}
