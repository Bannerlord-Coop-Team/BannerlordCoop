using Common;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Traits.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.LogEntries;

namespace GameInterface.Services.Traits.Handlers;

internal class PlayerTraitHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public PlayerTraitHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<NetworkPlayerTraitXpAdded>(Handle_NetworkPlayerTraitXpAdded);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkPlayerTraitXpAdded>(Handle_NetworkPlayerTraitXpAdded);
    }

    private void Handle_NetworkPlayerTraitXpAdded(MessagePayload<NetworkPlayerTraitXpAdded> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeroHandle, out var playerHero)) return;
            if (playerHero != Hero.MainHero) return;

            if (!objectManager.TryGetObjectWithLogging<TraitObject>(data.TraitHandle, out var trait)) return;

            if (data.LevelChanged)
            {
                CampaignEventDispatcher.Instance.OnPlayerTraitChanged(trait, data.PreviousLevel);
            }

            if (!data.AddLogEntry) return;

            Hero referenceHero = null;
            if (data.ReferenceHeroHandle != 0 && !objectManager.TryGetObjectWithLogging(data.ReferenceHeroHandle, out referenceHero)) return;

            LogEntry.AddLogEntry(new PlayerReputationChangesLogEntry(trait, referenceHero, data.Note));
        });
    }
}
