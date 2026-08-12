using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Heroes.Patches.Disable;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

public sealed class ConfiguredMinorFactionDataTests
{
    private const string CoopTeamLeaderId = "coop_team_lord_another_joke";

    private static readonly (string Id, string Name, string NativeTemplate)[] FoundingLords =
    {
        ("coop_team_lord_hasted", "Hasted", "spc_skolderbrotva_leader_0"),
        ("coop_team_lord_another_joke", "AnotherJoke", "spc_skolderbrotva_leader_1"),
        ("coop_team_lord_curzek", "Curzek", "spc_skolderbrotva_leader_2"),
        ("coop_team_lord_shotup", "ShotUp", "spc_skolderbrotva_leader_3"),
    };

    [Fact]
    public void ApplyConfiguredMinorFactionLordName_UsesTemplateNameForFirstAndFullName()
    {
        var template = new CharacterObject
        {
            StringId = FoundingLords[0].Id,
            _basicName = new TextObject(FoundingLords[0].Name),
            _occupation = Occupation.Lord,
        };
        var generatedCharacter = new CharacterObject
        {
            _originCharacter = template,
        };
        var hero = new Hero
        {
            _characterObject = generatedCharacter,
            _name = new TextObject("Random Sturgian Name"),
            _firstName = new TextObject("Random"),
        };

        HeroCreatorPatches.ApplyConfiguredMinorFactionLordName(hero);

        Assert.Equal(FoundingLords[0].Name, hero.Name.ToString());
        Assert.Equal(FoundingLords[0].Name, hero.FirstName.ToString());
    }

    [Fact]
    public void ApplyConfiguredMinorFactionLordName_UsesExplicitCreationTemplate()
    {
        var configuredTemplate = new CharacterObject
        {
            StringId = FoundingLords[0].Id,
            _basicName = new TextObject(FoundingLords[0].Name),
            _occupation = Occupation.Lord,
        };
        var hero = new Hero
        {
            _characterObject = new CharacterObject
            {
                _originCharacter = new CharacterObject { StringId = "different_template" },
            },
            _name = new TextObject("Random Sturgian Name"),
            _firstName = new TextObject("Random"),
        };

        HeroCreatorPatches.ApplyConfiguredMinorFactionLordName(hero, configuredTemplate);

        Assert.Equal(FoundingLords[0].Name, hero.Name.ToString());
        Assert.Equal(FoundingLords[0].Name, hero.FirstName.ToString());
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void ShouldApplyConfiguredName_RequiresServerOrNativeAuthority(
        bool isServer,
        bool originalAllowed,
        bool expected)
    {
        Assert.Equal(
            expected,
            HeroCreatorPatches.ShouldApplyConfiguredName(isServer, originalAllowed));
    }

    [Fact]
    public void HeroCreatorPatch_TargetsExactCreateSpecialHeroOverload()
    {
        var harmony = new Harmony($"{nameof(HeroCreatorPatch_TargetsExactCreateSpecialHeroOverload)}.{Guid.NewGuid()}");
        try
        {
            Type[] expectedParameterTypes =
            {
                typeof(CharacterObject),
                typeof(TaleWorlds.CampaignSystem.Settlements.Settlement),
                typeof(Clan),
                typeof(Clan),
                typeof(int),
            };

            var patched = harmony.CreateClassProcessor(typeof(HeroCreatorPatches)).Patch();

            Assert.Contains(
                patched,
                method => method.Name.Contains(nameof(HeroCreator.CreateSpecialHero), StringComparison.Ordinal) &&
                          method.GetParameters()
                              .Select(parameter => parameter.ParameterType)
                              .SequenceEqual(expectedParameterTypes));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void CoopTeamMinorFactionSpawn_OnlyAllowsNewCampaignFill()
    {
        var coopTeam = new Clan { StringId = HeroSpawnCampaignBehaviorPatches.CoopTeamClanId };
        var otherClan = new Clan { StringId = "other_minor_faction" };

        Assert.True(HeroSpawnCampaignBehaviorPatches.SpawnMinorFactionHeroesPrefix(
            coopTeam,
            firstTime: true));
        Assert.False(HeroSpawnCampaignBehaviorPatches.SpawnMinorFactionHeroesPrefix(
            coopTeam,
            firstTime: false));
        Assert.True(HeroSpawnCampaignBehaviorPatches.SpawnMinorFactionHeroesPrefix(
            otherClan,
            firstTime: false));
    }

    [Fact]
    public void CoopTeam_IsRegisteredWithAnotherJokeLeadingConfiguredLordTemplates()
    {
        string repositoryRoot = GetRepositoryRoot();
        string moduleData = Path.Combine(repositoryRoot, "deploy", "ModuleData");

        XElement factionRoot = XDocument.Load(Path.Combine(moduleData, "coop_minor_factions.xml")).Root
            ?? throw new InvalidDataException("coop_minor_factions.xml has no root element");
        XElement faction = factionRoot.Elements("Faction").Single();

        Assert.Equal("coop_team", faction.Attribute("id")?.Value);
        Assert.Equal("{=!}Coop Team", faction.Attribute("name")?.Value);
        Assert.Equal("true", faction.Attribute("is_minor_faction")?.Value);
        Assert.Equal("true", faction.Attribute("is_clan_type_mercenary")?.Value);
        Assert.Equal("Culture.sturgia", faction.Attribute("culture")?.Value);
        Assert.Equal("Settlement.town_S1", faction.Attribute("initial_home_settlement")?.Value);
        Assert.Equal(
            "PartyTemplate.kingdom_hero_party_mercenary_sturgia_template",
            faction.Attribute("default_party_template")?.Value);
        Assert.Equal("4", faction.Attribute("tier")?.Value);
        string[] factionLordIds = faction.Element("minor_faction_character_templates")?
            .Elements("template")
            .Select(template => template.Attribute("id")?.Value
                ?? throw new InvalidDataException("Minor faction template has no id"))
            .ToArray()
            ?? Array.Empty<string>();
        Assert.True(factionLordIds.Length >= new DefaultMinorFactionsModel().MinorFactionHeroLimit);
        Assert.Equal($"NPCCharacter.{CoopTeamLeaderId}", factionLordIds[0]);
        foreach ((string id, _, _) in FoundingLords)
        {
            Assert.Contains($"NPCCharacter.{id}", factionLordIds);
        }

        XElement lordRoot = XDocument.Load(Path.Combine(moduleData, "coop_minor_faction_lords.xml")).Root
            ?? throw new InvalidDataException("coop_minor_faction_lords.xml has no root element");
        Dictionary<string, XElement> lordTemplates = lordRoot
            .Elements("NPCCharacter")
            .ToDictionary(
                lord => lord.Attribute("id")?.Value
                    ?? throw new InvalidDataException("Minor faction lord template has no id"),
                StringComparer.Ordinal);
        Assert.Equal(
            factionLordIds.OrderBy(id => id, StringComparer.Ordinal),
            lordTemplates.Keys
                .Select(id => $"NPCCharacter.{id}")
                .OrderBy(id => id, StringComparer.Ordinal));

        foreach ((string id, XElement lord) in lordTemplates)
        {
            Assert.Equal("true", lord.Attribute("is_template")?.Value);
            Assert.Equal("false", lord.Attribute("is_hero")?.Value);
            Assert.Equal("false", lord.Attribute("is_female")?.Value);
            Assert.Equal("Culture.sturgia", lord.Attribute("culture")?.Value);
            Assert.Equal("Lord", lord.Attribute("occupation")?.Value);
            Assert.False(string.IsNullOrWhiteSpace(lord.Attribute("name")?.Value));
            Assert.False(string.IsNullOrWhiteSpace(lord.Attribute("skill_template")?.Value));
            Assert.StartsWith(
                HeroCreatorPatches.ConfiguredMinorFactionLordPrefix,
                id,
                StringComparison.Ordinal);

            XElement[] equipmentSets = lord.Element("Equipments")?
                .Elements("EquipmentSet")
                .ToArray()
                ?? Array.Empty<XElement>();
            Assert.Contains(
                equipmentSets,
                equipment => equipment.Attribute("equipmentType") == null);
            Assert.Contains(
                equipmentSets,
                equipment => equipment.Attribute("equipmentType")?.Value == "Civilian");
        }

        foreach ((string id, string name, string nativeTemplate) in FoundingLords)
        {
            XElement lord = lordTemplates[id];
            Assert.Equal($"{{=!}}{name}", lord.Attribute("name")?.Value);
            Assert.Equal($"SkillSet.{nativeTemplate}_skills", lord.Attribute("skill_template")?.Value);

            XElement[] equipmentSets = lord.Element("Equipments")?
                .Elements("EquipmentSet")
                .ToArray()
                ?? Array.Empty<XElement>();
            Assert.Contains(
                equipmentSets,
                equipment => equipment.Attribute("id")?.Value == nativeTemplate &&
                             equipment.Attribute("equipmentType") == null);
            Assert.Contains(
                equipmentSets,
                equipment => equipment.Attribute("id")?.Value == nativeTemplate &&
                             equipment.Attribute("equipmentType")?.Value == "Civilian");
        }

        XElement xmlRegistrations = XDocument.Load(Path.Combine(repositoryRoot, "deploy", "SubModule.xml"))
            .Root?
            .Element("Xmls")
            ?? throw new InvalidDataException("SubModule.xml has no Xmls element");
        Dictionary<string, XElement> registrations = xmlRegistrations
            .Elements("XmlNode")
            .ToDictionary(
                node => node.Element("XmlName")?.Attribute("path")?.Value
                    ?? throw new InvalidDataException("SubModule XmlNode has no path"),
                node => node,
                StringComparer.Ordinal);

        AssertRegistration(registrations["coop_minor_faction_lords"], "NPCCharacters");
        AssertRegistration(registrations["coop_minor_factions"], "Factions");
    }

    private static void AssertRegistration(XElement registration, string expectedId)
    {
        Assert.Equal(expectedId, registration.Element("XmlName")?.Attribute("id")?.Value);
        Assert.Equal(
            new[] { "Campaign", "CampaignStoryMode" },
            registration.Element("IncludedGameTypes")?
                .Elements("GameType")
                .Select(gameType => gameType.Attribute("value")?.Value));
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            ".."));
    }
}
