using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using SandBox.GauntletUI;
using TaleWorlds.CampaignSystem;
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

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeroStringId, out var hero)) return;

            HeroDataPatches.SetNameOverride(hero, data.FullName, data.FirstName);

            if (ScreenManager.TopScreen is GauntletClanScreen clanScreen)
            {
                clanScreen._dataSource?.RefreshValues();
            }
        });
    }
}
