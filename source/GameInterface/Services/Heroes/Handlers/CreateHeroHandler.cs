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
internal class CreateHeroHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CreateHeroHandler>();

    private readonly IHeroInterface heroInterface;
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public CreateHeroHandler(
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
        var data = obj.What.HeroCreationData;

        if (objectManager.TryGetObject(data.TemplateStringId, out CharacterObject template) == false)
        {
            Logger.Error("Unable to get {type} from {id}", typeof(Settlement).Name, data.BornSettlementId);
        }

        if (objectManager.TryGetObject(data.BornSettlementId, out Settlement bornSettlement) == false)
        {
            Logger.Error("Unable to get {type} from {id}", typeof(Settlement).Name, data.BornSettlementId);
        }

        
        CampaignTime birthDay = new CampaignTime();
        birthDay.SetNumTicks(data.Birthday);

        HeroCreationDeletionPatches.OverrideCreateNewHero(template, data.Age, birthDay, bornSettlement);
    }
}
