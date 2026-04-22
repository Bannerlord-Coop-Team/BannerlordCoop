using Common.Logging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;

namespace GameInterface.Services.CharacterDevelopers.Interfaces;

public interface ICharacterDeveloperInterface : IGameAbstraction {
    CharacterDeveloperHeroItemVM UnpackCharacterDeveloperHeroItemVM();
}

internal class CharacterDeveloperInterface : ICharacterDeveloperInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<CharacterDeveloperInterface>();
    private readonly IObjectManager objectManager;
    private readonly IBinaryPackageFactory binaryPackageFactory;
    private readonly IControlledEntityRegistry entityRegistry;

    public CharacterDeveloperHeroItemVM UnpackCharacterDeveloperHeroItemVM()
    {
        // Unpack and get skills, attributes and perks from CharacterDeveloperHeroItemVM?
        //

        return null;
    }
}