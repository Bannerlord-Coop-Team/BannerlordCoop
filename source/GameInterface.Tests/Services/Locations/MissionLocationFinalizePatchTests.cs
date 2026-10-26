using Common.Util;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Patches;
using GameInterface.Tests.Bootstrap;
using HarmonyLib;
using Moq;
using SandBox.Missions.MissionLogics;
using System;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Locations;

[Collection(nameof(MissionCurrentCollection))]
public sealed class MissionLocationFinalizePatchTests
{
    [Fact]
    public void FinalizeAfterSettlementEncounterWasCleared_DoesNotThrow()
    {
        Campaign previousCampaign = Campaign.Current;
        Campaign campaign = ObjectHelper.SkipConstructor<Campaign>();
        Campaign.Current = campaign;
        try
        {
            campaign.LocationEncounter = null;
            var logic = (MissionLocationLogic)FormatterServices.GetUninitializedObject(typeof(MissionLocationLogic));

            Assert.False(MissionLocationFinalizePatch.Prefix(logic));
        }
        finally
        {
            Campaign.Current = previousCampaign;
        }
    }

    [Fact]
    public void FinalizeAfterSettlementEncounterWasCleared_CleansFallbackRosterAndUsesInstalledPatch()
    {
        GameBootStrap.Initialize();
        var complex = new LocationComplex();
        var tavern = CreateLocation("tavern", complex);
        complex._locations.Add(tavern.StringId, tavern);

        Hero hero = CreateHero("finalize-hero");
        tavern._characterList.Add(CreateLocationCharacter(hero));
        var npc = new CharacterObject { StringId = "finalize-npc" };
        tavern._characterList.Add(LocationCharacterFactory.Create(
            npc,
            originParty: null,
            specialItem: null,
            spawnTag: "npc_common",
            actionSetCode: null,
            behaviorsMethodName: null,
            characterRelation: (int)LocationCharacter.CharacterRelations.Neutral,
            fixedLocation: false,
            useCivilianEquipment: true));

        var campaignMission = new Mock<ICampaignMission>();
        campaignMission.SetupGet(value => value.Location).Returns(tavern);
        ICampaignMission previousMission = CampaignMission.Current;
        Campaign previousCampaign = Campaign.Current;
        CampaignMission.Current = campaignMission.Object;
        Campaign.Current = ObjectHelper.SkipConstructor<Campaign>();
        Campaign.Current.LocationEncounter = null;

        var logic = (MissionLocationLogic)FormatterServices.GetUninitializedObject(typeof(MissionLocationLogic));
        var harmony = new Harmony($"{nameof(MissionLocationFinalizePatchTests)}.{Guid.NewGuid():N}");
        try
        {
            var patched = harmony.CreateClassProcessor(typeof(MissionLocationFinalizePatch)).Patch();
            Assert.Contains(patched, method => method.Name == nameof(MissionLocationLogic.OnRemoveBehavior));

            logic.OnRemoveBehavior();

            var remaining = Assert.Single(tavern.GetCharacterList());
            Assert.Same(hero.CharacterObject, remaining.Character);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            CampaignMission.Current = previousMission;
            Campaign.Current = previousCampaign;
        }
    }

    private static Hero CreateHero(string id)
    {
        var hero = new Hero { StringId = id };
        var character = new CharacterObject
        {
            StringId = id + "-character",
            BodyPropertyRange = new MBBodyProperty(),
        };
        character.HeroObject = hero;
        hero._characterObject = character;
        return hero;
    }

    private static Location CreateLocation(string id, LocationComplex locationComplex)
        => new(
            id,
            new TextObject(id),
            new TextObject(id),
            100,
            true,
            false,
            "CanAlways",
            "CanAlways",
            "CanAlways",
            "CanAlways",
            new string[4],
            locationComplex);

    private static LocationCharacter CreateLocationCharacter(Hero hero)
        => LocationCharacterFactory.Create(
            hero.CharacterObject,
            originParty: null,
            specialItem: null,
            spawnTag: "npc_common",
            actionSetCode: null,
            behaviorsMethodName: null,
            characterRelation: (int)LocationCharacter.CharacterRelations.Neutral,
            fixedLocation: false,
            useCivilianEquipment: true);
}
