using GameInterface.Services.Heroes.Patches;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Companions;

public sealed class ConfiguredWandererDataTests
{
    private const string HastedId = "coop_wanderer_hasted";

    [Fact]
    public void ApplyConfiguredWandererName_UsesTemplateNameForFirstAndFullName()
    {
        var template = new CharacterObject
        {
            StringId = HastedId,
            _basicName = new TextObject("Hasted"),
            _occupation = Occupation.Wanderer,
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

        HeroCreatorPatches.ApplyConfiguredWandererName(hero);

        Assert.Equal("Hasted", hero.Name.ToString());
        Assert.Equal("Hasted", hero.FirstName.ToString());
    }

    [Fact]
    public void Hasted_IsRegisteredAsNativeNamedWandererWithCompleteIntroduction()
    {
        string repositoryRoot = GetRepositoryRoot();
        string moduleData = Path.Combine(repositoryRoot, "deploy", "ModuleData");

        XElement wandererRoot = XDocument.Load(Path.Combine(moduleData, "coop_wanderers.xml")).Root
            ?? throw new InvalidDataException("coop_wanderers.xml has no root element");
        XElement hasted = wandererRoot
            .Elements("NPCCharacter")
            .Single(character => character.Attribute("id")?.Value == HastedId);

        Assert.Equal("{=!}Hasted", hasted.Attribute("name")?.Value);
        Assert.Equal("true", hasted.Attribute("is_template")?.Value);
        Assert.Equal("false", hasted.Attribute("is_hero")?.Value);
        Assert.Equal("false", hasted.Attribute("is_female")?.Value);
        Assert.Equal("Culture.sturgia", hasted.Attribute("culture")?.Value);
        Assert.Equal("Wanderer", hasted.Attribute("occupation")?.Value);
        Assert.Equal("SkillSet.spc_wanderer_sturgia_4_skills", hasted.Attribute("skill_template")?.Value);
        Assert.Null(hasted.Attribute("coop_settlement"));
        Assert.Null(hasted.Attribute("coop_location"));
        Assert.StartsWith(HeroCreatorPatches.ConfiguredWandererPrefix, HastedId, StringComparison.Ordinal);

        Assert.Contains(
            hasted.Element("Equipments")?.Elements("EquipmentRoster") ?? Enumerable.Empty<XElement>(),
            equipment => equipment.Attribute("civilian")?.Value == "true");
        Assert.Contains(
            hasted.Element("Equipments")?.Elements("EquipmentSet") ?? Enumerable.Empty<XElement>(),
            equipment => equipment.Attribute("id")?.Value == "npc_companion_equipment_template_sturgia" &&
                         equipment.Attribute("equipmentType") == null);

        XElement introductionRoot = XDocument.Load(Path.Combine(moduleData, "coop_wanderer_strings.xml")).Root
            ?? throw new InvalidDataException("coop_wanderer_strings.xml has no root element");
        var introductionIds = introductionRoot
            .Elements("string")
            .Select(text => text.Attribute("id")?.Value
                ?? throw new InvalidDataException("Configured wanderer string has no id"))
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(
            new[]
            {
                "prebackstory",
                "backstory_a",
                "backstory_b",
                "backstory_c",
                "response_1",
                "response_2",
                "backstory_d",
                "generic_backstory",
            },
            prefix => Assert.Contains($"{prefix}.{HastedId}", introductionIds));

        XElement fixedNpcRoot = XDocument.Load(Path.Combine(moduleData, "coop_fixed_town_npcs.xml")).Root
            ?? throw new InvalidDataException("coop_fixed_town_npcs.xml has no root element");
        XElement fixedHasted = fixedNpcRoot
            .Elements("NPCCharacter")
            .Single(character => character.Attribute("id")?.Value == "coop_fixed_npc_hasted");
        Assert.Equal("true", fixedHasted.Attribute("coop_enabled")?.Value);

        XElement xmlRegistrations = XDocument.Load(Path.Combine(repositoryRoot, "deploy", "SubModule.xml"))
            .Root?
            .Element("Xmls")
            ?? throw new InvalidDataException("SubModule.xml has no Xmls element");
        var registrations = xmlRegistrations
            .Elements("XmlNode")
            .ToDictionary(
                node => node.Element("XmlName")?.Attribute("path")?.Value
                    ?? throw new InvalidDataException("SubModule XmlNode has no path"),
                node => node,
                StringComparer.Ordinal);

        AssertRegistration(registrations["coop_wanderers"], "NPCCharacters");
        AssertRegistration(registrations["coop_wanderer_strings"], "GameText");
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
