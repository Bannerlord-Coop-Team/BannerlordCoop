using GameInterface.Services.Locations;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using Moq;
using Serilog;
using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Locations;

public sealed class FixedTownNpcServiceTests : IDisposable
{
    private readonly string tempDirectory;

    public FixedTownNpcServiceTests()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "fixed-town-npc-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
    }

    public void Dispose()
    {
        try { Directory.Delete(tempDirectory, recursive: true); } catch { }
    }

    [Fact]
    public void ReadDefinitions_ReadsPlacementAndAppliesDefaults()
    {
        string path = WriteXml(@"
<NPCCharacters>
  <NPCCharacter id=""coop_alice"" coop_settlement=""town_V1"" />
  <NPCCharacter
    id=""coop_bob""
    coop_settlement=""town_V2""
    coop_location=""center""
    coop_spawn_tag=""sp_notable""
    coop_dialogue=""Custom greeting"" />
</NPCCharacters>");

        var definitions = FixedTownNpcService.ReadDefinitions(path, new Mock<ILogger>().Object);

        Assert.Collection(
            definitions,
            definition =>
            {
                Assert.Equal("coop_alice", definition.CharacterId);
                Assert.Equal("town_V1", definition.SettlementId);
                Assert.Equal("tavern", definition.LocationId);
                Assert.Equal("npc_common", definition.SpawnTag);
                Assert.Null(definition.Dialogue);
            },
            definition =>
            {
                Assert.Equal("coop_bob", definition.CharacterId);
                Assert.Equal("town_V2", definition.SettlementId);
                Assert.Equal("center", definition.LocationId);
                Assert.Equal("sp_notable", definition.SpawnTag);
                Assert.Equal("Custom greeting", definition.Dialogue);
            });
    }

    [Fact]
    public void IsConversationCharacter_MatchesOnlyConfiguredCharacter()
    {
        var hasted = new CharacterObject { StringId = "coop_fixed_npc_hasted" };
        var other = new CharacterObject { StringId = "other_character" };

        Assert.True(FixedTownNpcConversationBehavior.IsConversationCharacter(
            "coop_fixed_npc_hasted",
            hasted));
        Assert.False(FixedTownNpcConversationBehavior.IsConversationCharacter(
            "coop_fixed_npc_hasted",
            other));
        Assert.False(FixedTownNpcConversationBehavior.IsConversationCharacter(
            "coop_fixed_npc_hasted",
            null));
    }

    [Fact]
    public void ReadDefinitions_SkipsUnplacedDisabledInvalidAndDuplicateEntries()
    {
        string path = WriteXml(@"
<NPCCharacters>
  <NPCCharacter id=""ordinary_character"" />
  <NPCCharacter id=""disabled"" coop_settlement=""town_V1"" coop_enabled=""false"" />
  <NPCCharacter id=""invalid"" coop_settlement=""town_V1"" coop_enabled=""sometimes"" />
  <NPCCharacter id=""duplicate"" coop_settlement=""town_V1"" />
  <NPCCharacter id=""duplicate"" coop_settlement=""town_V2"" />
</NPCCharacters>");

        var definitions = FixedTownNpcService.ReadDefinitions(path, new Mock<ILogger>().Object);

        var definition = Assert.Single(definitions);
        Assert.Equal("duplicate", definition.CharacterId);
        Assert.Equal("town_V1", definition.SettlementId);
    }

    [Fact]
    public void Populate_AddsMatchingCharacterOnceToConfiguredLocation()
    {
        GameBootStrap.Initialize();

        string path = WriteXml(@"
<NPCCharacters>
  <NPCCharacter id=""coop_alice"" coop_settlement=""town_test"" />
  <NPCCharacter id=""coop_other"" coop_settlement=""town_other"" />
</NPCCharacters>");

        var character = new CharacterObject
        {
            StringId = "coop_alice",
            BodyPropertyRange = new MBBodyProperty(),
        };
        var objectManager = new Mock<IObjectManager>();
        objectManager
            .Setup(manager => manager.TryGetObject("coop_alice", out character))
            .Returns(true);

        var locationComplex = new LocationComplex();
        var tavern = CreateLocation("tavern", locationComplex);
        locationComplex._locations.Add(tavern.StringId, tavern);

        var settlement = new Settlement(new TextObject("Test Town"), locationComplex, null)
        {
            StringId = "town_test",
        };

        var service = new FixedTownNpcService(
            new Mock<ILogger>().Object,
            objectManager.Object,
            () => path);

        service.Populate(settlement);
        service.Populate(settlement);

        objectManager.Verify(
            manager => manager.TryGetObject("coop_alice", out character),
            Times.AtLeastOnce);

        var placed = Assert.Single(tavern.GetCharacterList());
        Assert.Same(character, placed.Character);
        Assert.Equal("npc_common", placed.SpecialTargetTag);
        Assert.True(placed.FixedLocation);
        Assert.True(placed.UseCivilianEquipment);
    }

    private string WriteXml(string xml)
    {
        string path = Path.Combine(tempDirectory, FixedTownNpcService.XmlName + ".xml");
        File.WriteAllText(path, xml);
        return path;
    }

    private static Location CreateLocation(string id, LocationComplex locationComplex)
    {
        return new Location(
            stringId: id,
            name: new TextObject(id),
            doorName: new TextObject(id),
            prosperityMax: 100,
            isIndoor: true,
            canBeReserved: false,
            playerCanEnter: "CanAlways",
            playerCanSee: "CanAlways",
            aiCanExit: "CanAlways",
            aiCanEnter: "CanAlways",
            sceneNames: new string[4],
            locationComplex: locationComplex);
    }
}
