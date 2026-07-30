using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using SandBox.GauntletUI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Heroes.Handlers;
internal class HeroDataHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public HeroDataHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<ChangeHeroName>(Handle_HeroChangeName);
        
    }

    public void Dispose()
    {
        messageBroker?.Unsubscribe<ChangeHeroName>(Handle_HeroChangeName);
    }

    private void Handle_HeroChangeName(MessagePayload<ChangeHeroName> payload)
    {
        var data = payload.What.Data;

        // Resolve the hero on the game-loop thread, in queue order with the marshaled hero
        // creation — a network-thread lookup races a creation still waiting in the game-thread
        // queue and permanently drops this one-shot name, leaving a name-less hero. The clan
        // screen refresh must also run on the main thread.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeroStringId, out var hero)) return;

            var fullName = new TextObject(data.FullName);
            var firstName = new TextObject(data.FirstName);

            HeroDataPatches.SetNameOverride(hero, fullName, firstName);

            if (ScreenManager.TopScreen is GauntletClanScreen clanScreen)
            {
                clanScreen._dataSource?.RefreshValues();
            }
        });
    }
}
