﻿using Common.Logging;
using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Heroes.Interfaces;

internal interface IHeroInterface : IGameAbstraction
{
    byte[] PackageMainHero();
    void ResolveHero(ResolveHero message);
    void SwitchMainHero(string heroId);
    Hero UnpackMainHero(byte[] bytes);
}

internal class HeroInterface : IHeroInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroInterface>();
    private readonly IObjectManager objectManager;
    private readonly IBinaryPackageFactory binaryPackageFactory;
    private readonly IMessageBroker messageBroker;

    public HeroInterface(
        IObjectManager objectManager,
        IBinaryPackageFactory binaryPackageFactory,
        IMessageBroker messageBroker)
    {
        this.objectManager = objectManager;
        this.binaryPackageFactory = binaryPackageFactory;
        this.messageBroker = messageBroker;
    }

    public byte[] PackageMainHero()
    {
        HeroBinaryPackage package = binaryPackageFactory.GetBinaryPackage<HeroBinaryPackage>(Hero.MainHero);
        return BinaryFormatterSerializer.Serialize(package);
    }

    public Hero UnpackMainHero(byte[] bytes)
    {
        HeroBinaryPackage package = BinaryFormatterSerializer.Deserialize<HeroBinaryPackage>(bytes);
        return package.Unpack<Hero>(binaryPackageFactory);
    }

    public void ResolveHero(ResolveHero message)
    {
        // TODO implement
        messageBroker.Publish(this, new ResolveDebugHero(message.PlayerId));
    }

    public void SwitchMainHero(string heroId)
    {
        if(objectManager.TryGetObject(heroId, out Hero resolvedHero))
        {
            Logger.Information("Switching to new hero: {heroName}", resolvedHero.Name.ToString());

            ChangePlayerCharacterAction.Apply(resolvedHero);
        }
        else
        {
            Logger.Warning("Could not find hero with id of: {guid}", heroId);
        }
    }
}
