using GameInterface.Services.Heroes;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

public sealed class ConfiguredMinorFactionDataTests
{
    private static readonly (string Id, string Name, string NativeTemplate)[] FoundingLords =
    {
        ("coop_team_lord_hasted", "Hasted", "spc_skolderbrotva_leader_0"),
        ("coop_team_lord_another_joke", "AnotherJoke", "spc_skolderbrotva_leader_1"),
        ("coop_team_lord_curzek", "Curzek", "spc_skolderbrotva_leader_2"),
        ("coop_team_lord_shotup", "ShotUp", "spc_skolderbrotva_leader_3"),
    };

    [Theory]
    [InlineData("coop_team", true, true, true, true)]
    [InlineData("coop_team", false, true, true, false)]
    [InlineData("coop_team", true, false, false, false)]
    [InlineData("other_minor_faction", true, true, false, false)]
    public void SpawnPolicy_OnlyCreatesCoopTeamOnAuthoritativeNewCampaigns(
        string clanId,
        bool firstTime,
        bool hasAuthority,
        bool expectedReplacement,
        bool expectedCreation)
    {
        Assert.Equal(
            expectedReplacement,
            ConfiguredMinorFactionHeroSpawner.ShouldReplaceNativeSpawn(clanId, hasAuthority));
        Assert.Equal(
            expectedCreation,
            ConfiguredMinorFactionHeroSpawner.ShouldCreateInitialHeroes(
                clanId,
                firstTime,
                hasAuthority));
    }

    [Fact]
    public void EnabledTemplates_AreSelectedInConfigurationOrderWithoutFourLordLimit()
    {
        CharacterObject[] configuredTemplates =
        {
            CreateTemplate(ConfiguredMinorFactionHeroSpawner.CoopTeamLeaderTemplateId, enabled: true),
            CreateTemplate("coop_team_lord_disabled", enabled: false),
            CreateTemplate("coop_team_lord_2", enabled: true),
            CreateTemplate("coop_team_lord_3", enabled: true),
            CreateTemplate("coop_team_lord_4", enabled: true),
            CreateTemplate("coop_team_lord_added_later", enabled: true),
        };

        IReadOnlyList<CharacterObject> enabled =
            ConfiguredMinorFactionHeroSpawner.GetEnabledTemplates(configuredTemplates);

        Assert.Equal(
            new[]
            {
                ConfiguredMinorFactionHeroSpawner.CoopTeamLeaderTemplateId,
                "coop_team_lord_2",
                "coop_team_lord_3",
                "coop_team_lord_4",
                "coop_team_lord_added_later",
            },
            enabled.Select(template => template.StringId));
    }

    [Fact]
    public void BootCategory_PatchesNativeMinorFactionSpawnAtFirstPriority()
    {
        var harmony = new Harmony(nameof(BootCategory_PatchesNativeMinorFactionSpawnAtFirstPriority));
        var original = AccessTools.Method(
            typeof(HeroSpawnCampaignBehavior),
            nameof(HeroSpawnCampaignBehavior.SpawnMinorFactionHeroes),
            new[] { typeof(Clan), typeof(bool) });

        try
        {
            harmony.PatchCategory(
                typeof(ConfiguredMinorFactionHeroSpawner).Assembly,
                GameInterface.HARMONY_CONFIGURED_MINOR_FACTION_CATEGORY);

            Patch patch = Assert.Single(
                Harmony.GetPatchInfo(original).Prefixes,
                candidate => candidate.owner == harmony.Id);
            Assert.Equal(Priority.First, patch.priority);
            Assert.Equal(typeof(ConfiguredMinorFactionHeroSpawner), patch.PatchMethod.DeclaringType);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void CoopTeam_IsRegisteredWithAnotherJokeLeadingConfiguredLordTemplates()
    {
        string repositoryRoot = GetRepositoryRoot();
        string moduleData = Path.Combine(repositoryRoot, "deploy", "ModuleData");

        XElement factionRoot = XDocument.Load(Path.Combine(moduleData, "coop_minor_factions.xml")).Root
            ?? throw new InvalidDataException("coop_minor_factions.xml has no root element");
        XElement faction = factionRoot.Elements("Faction").Single();

        Assert.Equal(ConfiguredMinorFactionHeroSpawner.CoopTeamClanId, faction.Attribute("id")?.Value);
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
        Assert.Equal(
            $"NPCCharacter.{ConfiguredMinorFactionHeroSpawner.CoopTeamLeaderTemplateId}",
            factionLordIds[0]);
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
            Assert.True(bool.TryParse(lord.Attribute("is_template")?.Value, out _));
            Assert.Equal("false", lord.Attribute("is_hero")?.Value);
            Assert.Equal("false", lord.Attribute("is_female")?.Value);
            Assert.Equal("Culture.sturgia", lord.Attribute("culture")?.Value);
            Assert.Equal("Lord", lord.Attribute("occupation")?.Value);
            Assert.False(string.IsNullOrWhiteSpace(lord.Attribute("name")?.Value));
            Assert.False(string.IsNullOrWhiteSpace(lord.Attribute("skill_template")?.Value));
            Assert.StartsWith(
                ConfiguredMinorFactionHeroSpawner.ConfiguredLordPrefix,
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

        Assert.Equal(
            "true",
            lordTemplates[ConfiguredMinorFactionHeroSpawner.CoopTeamLeaderTemplateId]
                .Attribute("is_template")?.Value);

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

    private static CharacterObject CreateTemplate(string id, bool enabled)
    {
        var template = new CharacterObject
        {
            StringId = id,
            IsTemplate = enabled,
        };
        return template;
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
