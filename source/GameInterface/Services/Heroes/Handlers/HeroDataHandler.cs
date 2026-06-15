using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Clans;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Template.Handlers;
using GameInterface.Services.Template.Messages;
using GameInterface.Services.Template.Patches;
using SandBox.GauntletUI;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Heroes.Handlers;
internal class HeroDataHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroDataHandler>();

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

        if (objectManager.TryGetObject<Hero>(data.HeroStringId, out var hero) == false)
        {
            Logger.Error("Unable to get {type} from id {stringId}", typeof(Hero), data.HeroStringId);
            return;
        }

        // Applying the name runs vanilla game code and the refresh touches the clan screen
        // UI; both must run on the main thread, not the network thread that delivered it.
        GameLoopRunner.RunOnMainThread(() =>
        {
            if (Campaign.Current == null) return;

            try
            {
                var fullName = new TextObject(data.FullName);
                var firstName = new TextObject(data.FirstName);

                HeroDataPatches.SetNameOverride(hero, fullName, firstName);

                InformationManager.DisplayMessage(new InformationMessage($"Changed hero name to {fullName}"));

                if (ScreenManager.TopScreen is GauntletClanScreen clanScreen)
                {
                    clanScreen._dataSource?.RefreshValues();
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply hero name change for {stringId}", data.HeroStringId);
            }
        });
    }
}
