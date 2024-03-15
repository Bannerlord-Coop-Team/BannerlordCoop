using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Template.Handlers;
using GameInterface.Services.Template.Messages;
using GameInterface.Services.Template.Patches;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

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
        messageBroker.Subscribe<AddNewChildren>(Handle_AddChildren);

    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangeHeroName>(Handle_HeroChangeName);
        messageBroker.Unsubscribe<AddNewChildren>(Handle_AddChildren);
    }
    private void Handle_AddChildren(MessagePayload<AddNewChildren> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObject<Hero>(obj.HeroId, out var father) == false)
        {
            Logger.Error("Unable to get {type} from id {stringId}", typeof(Hero), obj.HeroId);
            return;
        }

        if (objectManager.TryGetObject<Hero>(obj.ChildId, out var child) == false)
        {
            Logger.Error("Unable to get {type} from id {stringId}", typeof(Hero), obj.HeroId);
            return;
        }

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                father._children.Add(child);
            }
        });

    }
    private void Handle_HeroChangeName(MessagePayload<ChangeHeroName> payload)
    {
        var data = payload.What.Data;

        if (objectManager.TryGetObject<Hero>(data.HeroStringId, out var hero) == false)
        {
            Logger.Error("Unable to get {type} from id {stringId}", typeof(Hero), data.HeroStringId);
            return;
        }

        var fullName = new TextObject(data.FullName);
        var firstName = new TextObject(data.FirstName);

        HeroDataPatches.SetNameOverride(hero, fullName, firstName);
    }
}
