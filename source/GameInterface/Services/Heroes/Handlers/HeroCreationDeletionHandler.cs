using Common.Logging;
using Common.Messaging;
using Coop.Mod.Extentions;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Heroes.Handlers;
internal class HeroCreationDeletionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroCreationDeletionHandler>();

    private readonly IHeroInterface heroInterface;
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public HeroCreationDeletionHandler(
        IHeroInterface heroInterface,
        IMessageBroker messageBroker,
        IObjectManager objectManager)
    {
        this.heroInterface = heroInterface;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        messageBroker.Subscribe<CreateHero>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CreateHero>(Handle);
    }

    private void Handle(MessagePayload<CreateHero> obj)
    {
        var data = obj.What.Data;
        HeroCreationDeletionPatches.OverrideCreateNewHero(data.HeroStringId);

        messageBroker.Publish(this, new HeroCreated(data));
    }
}
