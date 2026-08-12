using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Heroes.Patches.Disable;
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
    private static readonly (string Id, string Name, string NativeTemplate)[] Lords =
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
            StringId = Lords[0].Id,
            _basicName = new TextObject(Lords[0].Name),
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

        Assert.Equal(Lords[0].Name, hero.Name.ToString());
        Assert.Equal(Lords[0].Name, hero.FirstName.ToString());
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
    public void CoopTeam_IsRegisteredAsMinorFactionWithFourNamedLordTemplates()
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
        Assert.Equal(Lords.Length, new DefaultMinorFactionsModel().MinorFactionHeroLimit);

        Assert.Equal(
            Lords.Select(lord => $"NPCCharacter.{lord.Id}"),
            faction.Element("minor_faction_character_templates")?
                .Elements("template")
                .Select(template => template.Attribute("id")?.Value));

        XElement lordRoot = XDocument.Load(Path.Combine(moduleData, "coop_minor_faction_lords.xml")).Root
            ?? throw new InvalidDataException("coop_minor_faction_lords.xml has no root element");
        XElement[] lordTemplates = lordRoot.Elements("NPCCharacter").ToArray();
        Assert.Equal(Lords.Length, lordTemplates.Length);

        for (int index = 0; index < Lords.Length; index++)
        {
            (string id, string name, string nativeTemplate) = Lords[index];
            XElement lord = lordTemplates[index];

            Assert.Equal(id, lord.Attribute("id")?.Value);
            Assert.Equal($"{{=!}}{name}", lord.Attribute("name")?.Value);
            Assert.Equal("true", lord.Attribute("is_template")?.Value);
            Assert.Equal("false", lord.Attribute("is_hero")?.Value);
            Assert.Equal("false", lord.Attribute("is_female")?.Value);
            Assert.Equal("Culture.sturgia", lord.Attribute("culture")?.Value);
            Assert.Equal("Lord", lord.Attribute("occupation")?.Value);
            Assert.Equal($"SkillSet.{nativeTemplate}_skills", lord.Attribute("skill_template")?.Value);
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
