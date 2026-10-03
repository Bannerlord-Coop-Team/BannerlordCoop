using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Issues.Handlers;

internal sealed class ArtisanProductTraitHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IObjectManager objects;

    public ArtisanProductTraitHandler(IMessageBroker broker, IObjectManager objects)
    {
        this.broker = broker;
        this.objects = objects;
        broker.Subscribe<NetworkArtisanTraitProgress>(Handle);
    }

    public void Dispose() => broker.Unsubscribe<NetworkArtisanTraitProgress>(Handle);

    private void Handle(MessagePayload<NetworkArtisanTraitProgress> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero) || data.Traits == null) return;
            var traits = data.Traits.Select(entry => MBObjectManager.Instance.GetObject<TraitObject>(entry.TraitId)).ToArray();
            if (traits.Any(trait => trait == null)) return;
            var progress = new PropertyOwner<PropertyObject>();
            using (new AllowedThread())
            {
                for (var i = 0; i < traits.Length; i++)
                {
                    progress.SetPropertyValue(traits[i], data.Traits[i].Xp);
                    hero.SetTraitLevel(traits[i], data.Traits[i].Level);
                }
                GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.Set(hero, progress);
                if (hero == Hero.MainHero) Campaign.Current.PlayerTraitDeveloper = progress;
            }
        });
    }
}
