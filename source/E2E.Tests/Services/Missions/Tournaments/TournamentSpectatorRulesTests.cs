using GameInterface.Services.Tournaments.Data;
using Missions.Tournaments.Spectators;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace E2E.Tests.Services.Missions.Tournaments;

public class TournamentSpectatorRulesTests
{
    [Fact]
    public void EmpireLayout_ContainsExtractedBarriersAndUniqueSpawns()
    {
        Assert.True(TournamentSpectatorSceneLayouts.TryGet(
            TournamentSpectatorSceneLayouts.EmpireArenaScene,
            out var layout));

        Assert.Equal(56, layout.Barriers.Count);
        Assert.Equal(44, layout.Barriers.Count(data => data.PrefabName == "_barrier_16x04m"));
        Assert.Equal(12, layout.Barriers.Count(data => data.PrefabName == "_barrier_04x04m"));
        Assert.Equal(13, layout.Spawns.Count);
        Assert.Equal(Enumerable.Range(1, 13), layout.Spawns.Select(data => data.SpawnId));
        Assert.Equal(
            Enumerable.Range(1, 13).Select(id => $"Spawn {id}"),
            layout.Spawns.Select(data => data.Name));
        string barrierTransforms = string.Join("|", layout.Barriers.Select(BarrierTransform));
        Assert.Equal(
            "BB95F350F059A9A8DED5A7D525B3E13899359CDC011802D7517149FD7E549A8F",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(barrierTransforms))));
        Assert.Equal(13, layout.Spawns.Select(data => data.Position).Distinct().Count());
        Assert.Contains(layout.Spawns, data =>
            SamePosition(data.Position, new Vec3(330.756f, 448.657f, 4.045f)) &&
            Math.Abs(data.Rotation - -1.138f) < 0.001f);
        Assert.DoesNotContain(layout.Spawns, data =>
            SamePosition(data.Position, new Vec3(330.756f, 448.657f, 4.045f)) &&
            Math.Abs(data.Rotation - -2.095f) < 0.001f);
    }

    [Fact]
    public void EmpireSpawns_MatchAuthoredPositions()
    {
        Assert.True(TournamentSpectatorSceneLayouts.TryGet(
            TournamentSpectatorSceneLayouts.EmpireArenaScene,
            out var layout));
        TournamentSpectatorSpawnData[] original =
        {
            new(1, new Vec3(400.833f, 458.222f, 4.045f), 3.136f),
            new(2, new Vec3(442.656f, 448.405f, 4.045f), 2.077f),
            new(3, new Vec3(445.852f, 442.646f, 4.045f), 2.077f),
            new(4, new Vec3(442.886f, 424.708f, 4.045f), 1.070f),
            new(5, new Vec3(419.170f, 415.069f, 4.045f), 0f),
            new(6, new Vec3(407.946f, 415.177f, 4.045f), 0f),
            new(7, new Vec3(399.709f, 415.256f, 4.045f), 0f),
            new(8, new Vec3(373.324f, 414.999f, 4.045f), 0f),
            new(9, new Vec3(351.942f, 415.028f, 4.045f), 0f),
            new(10, new Vec3(329.728f, 425.905f, 4.045f), -1.138f),
            new(11, new Vec3(330.756f, 448.657f, 4.045f), -1.138f),
            new(12, new Vec3(349.860f, 457.740f, 4.045f), -2.095f),
            new(13, new Vec3(386.592f, 466.554f, 8.280f), -3.137f)
        };

        Assert.Equal(original.Length, layout.Spawns.Count);
        for (int i = 0; i < original.Length; i++)
        {
            TournamentSpectatorSpawnData source = original[i];
            Assert.True(SamePosition(source.Position, layout.Spawns[i].Position));
            Assert.Equal(source.SpawnId, layout.Spawns[i].SpawnId);
            Assert.Equal(source.Rotation, layout.Spawns[i].Rotation);
        }
    }

    [Theory]
    [InlineData("arena_aserai_a", 46, 9, "0C8D312C66553651B7E4554459E23BAC3A1B7999BC9B0D1935324D9FE5598773", "8E25B9EA1AC787427A1430B1CC01F0A6F567232016BCCF037AEF8B7BBB7AE9B8")]
    [InlineData("arena_battania_a", 0, 1, "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", "6B80D5BB44A47AA0046942609ECFDD723D730ABC2DF63EBDF4EFE6FE74BAD6BE")]
    [InlineData("arena_khuzait_a", 9, 3, "219EC3A787BDFA54B2583B3A7F86C37B55CE62BF5EB160CD7F33AFF3317D1A92", "B3B7C1414AC16BCB38E429BBA7C9367089884E9ABA7B039CD60A60809BE93DD7")]
    [InlineData("arena_sturgia_a", 22, 5, "5F6466A3EE2FA4984B22352CDD474E065C7A3F3FFE38601842DF3E609DB511EB", "3CDAFA63706B17521835E724621AFAB8A1E60A880E8925D28589F3B4E2B85179")]
    [InlineData("arena_vlandia_a", 21, 6, "C18BB4FAE893D0B2698EDC8E000DFF782667B5C3EDE5C798FCED1D3DCC2F4C9E", "208FCE832A25C11059300FC36519D3E84F587AB9B1E8FC2A52E24999FE65905C")]
    public void LayoutLookup_ContainsExtractedArenaData(
        string sceneName,
        int barrierCount,
        int spawnCount,
        string barrierHash,
        string spawnHash)
    {
        Assert.True(TournamentSpectatorSceneLayouts.TryGet(sceneName, out var layout));
        Assert.Equal(barrierCount, layout.Barriers.Count);
        Assert.Equal(spawnCount, layout.Spawns.Count);
        Assert.Equal(Enumerable.Range(1, spawnCount), layout.Spawns.Select(data => data.SpawnId));
        Assert.Equal(spawnCount, layout.Spawns.Select(data => data.Position).Distinct().Count());
        Assert.Equal(
            barrierHash,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Join("|", layout.Barriers.Select(BarrierTransform))))));
        Assert.Equal(
            spawnHash,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Join("|", layout.Spawns.Select(SpawnTransform))))));
    }

    [Fact]
    public void EligibleControllers_IncludesEntrantSpectatorsAndNonFightersOnly()
    {
        TournamentSessionSnapshot snapshot = Snapshot(TournamentSessionPhase.LiveMatch);

        string[] eligible = TournamentSpectatorAgentManager.GetEligibleControllers(snapshot);

        Assert.Equal(new[] { "eliminated", "spectator", "waiting" }, eligible);
    }

    [Fact]
    public void EligibleControllers_IsEmptyOutsideLiveMatch()
    {
        Assert.Empty(TournamentSpectatorAgentManager.GetEligibleControllers(
            Snapshot(TournamentSessionPhase.AwaitingChoices)));
    }

    [Fact]
    public void BarrierCollision_BlocksCrossingWithinWallBoundsOnly()
    {
        var barrier = new TournamentSpectatorBarrierData(
            "_barrier_16x04m",
            new Vec3(0f, 0f, 0f),
            0f,
            new Vec3(16f, 1f, 4f));

        Assert.True(TournamentSpectatorBarrierCollision.CrossesBarrier(
            new Vec3(0f, -2f, 1f),
            new Vec3(0f, 2f, 1f),
            barrier));
        Assert.False(TournamentSpectatorBarrierCollision.CrossesBarrier(
            new Vec3(10f, -2f, 1f),
            new Vec3(10f, 2f, 1f),
            barrier));
        Assert.False(TournamentSpectatorBarrierCollision.CrossesBarrier(
            new Vec3(0f, -2f, 6f),
            new Vec3(0f, 2f, 6f),
            barrier));
    }

    [Fact]
    public void BarrierCollision_PreservesSidewaysMovementWhileConstrainingCrossing()
    {
        var barrier = new TournamentSpectatorBarrierData(
            "_barrier_16x04m",
            new Vec3(0f, 0f, 0f),
            0f,
            new Vec3(16f, 1f, 4f));

        bool constrained = TournamentSpectatorBarrierCollision.TryConstrain(
            new Vec3(0f, -2f, 1f),
            new Vec3(3f, 2f, 1f),
            barrier,
            out Vec3 constrainedPosition);

        Assert.True(constrained);
        Assert.Equal(3f, constrainedPosition.x, 3);
        Assert.Equal(-0.55f, constrainedPosition.y, 3);
        Assert.Equal(1f, constrainedPosition.z, 3);
    }

    [Fact]
    public void OrangeItem_UsesNativeMeshAndIsRegisteredForSupportedGameTypes()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            ".."));
        string itemPath = Path.Combine(repositoryRoot, "deploy", "ModuleData", "tournament_spectator_throwables.xml");
        string subModulePath = Path.Combine(repositoryRoot, "deploy", "SubModule.xml");

        XElement item = XDocument.Load(itemPath).Root?.Element("Item");
        Assert.NotNull(item);
        Assert.Equal(TournamentSpectatorOrange.ItemId, item.Attribute("id")?.Value);
        Assert.Equal("foods_orange_a", item.Attribute("mesh")?.Value);
        Assert.Equal("bo_throwing_stone_01", item.Attribute("body_name")?.Value);
        Assert.Equal("foods_orange_a", item.Attribute("flying_mesh")?.Value);
        Assert.Null(item.Attribute("scale_factor"));
        XElement weapon = item.Element("ItemComponent")?.Element("Weapon");
        Assert.NotNull(weapon);
        Assert.Equal(TournamentSpectatorOrange.RefillAmount.ToString(), weapon.Attribute("stack_amount")?.Value);
        Assert.Equal("0", weapon.Attribute("thrust_damage")?.Value);
        Assert.Null(weapon.Attribute("trail_particle_name"));
        Assert.Null(weapon.Element("WeaponFlags")?.Attribute("AttachAmmoToVisual"));

        XElement xmlNode = XDocument.Load(subModulePath).Root?
            .Element("Xmls")?
            .Elements("XmlNode")
            .Single(node => node.Element("XmlName")?.Attribute("path")?.Value == "tournament_spectator_throwables");
        Assert.NotNull(xmlNode);
        Assert.Equal(
            new[] { "Campaign", "CampaignStoryMode", "CustomGame", "EditorGame" },
            xmlNode.Element("IncludedGameTypes")?
                .Elements("GameType")
                .Select(node => node.Attribute("value")?.Value));
    }
    [Fact]
    public void OrangeEquipment_PreservesCivilianClothingAndClearsOtherWeapons()
    {
        var oldWeapon = new ItemObject();
        var bodyArmor = new ItemObject();
        var orange = new ItemObject();
        var civilian = new Equipment(Equipment.EquipmentType.Civilian)
        {
            [EquipmentIndex.Weapon0] = new EquipmentElement(oldWeapon),
            [EquipmentIndex.Weapon1] = new EquipmentElement(oldWeapon),
            [EquipmentIndex.Body] = new EquipmentElement(bodyArmor)
        };

        Equipment equipment = TournamentSpectatorOrange.BuildEquipment(civilian, orange);

        Assert.Same(orange, equipment[EquipmentIndex.Weapon0].Item);
        Assert.Null(equipment[EquipmentIndex.Weapon1].Item);
        Assert.Null(equipment[EquipmentIndex.Weapon2].Item);
        Assert.Null(equipment[EquipmentIndex.Weapon3].Item);
        Assert.Same(bodyArmor, equipment[EquipmentIndex.Body].Item);
    }

    [Fact]
    public void OrangeEquipment_MissingItemLeavesSpectatorUnarmed()
    {
        var oldWeapon = new ItemObject();
        var civilian = new Equipment(Equipment.EquipmentType.Civilian)
        {
            [EquipmentIndex.Weapon0] = new EquipmentElement(oldWeapon)
        };

        Equipment equipment = TournamentSpectatorOrange.BuildEquipment(civilian, null);

        for (int i = (int)EquipmentIndex.WeaponItemBeginSlot;
             i < (int)EquipmentIndex.NumAllWeaponSlots;
             i++)
        {
            Assert.Null(equipment[(EquipmentIndex)i].Item);
        }
    }

    [Fact]
    public void OrangeRefillRules_RefillOnlyAfterSingleOrangeIsConsumed()
    {
        var orange = new ItemObject();
        var other = new ItemObject();

        Assert.True(TournamentSpectatorOrange.ShouldRefill(orange, orange, 0));
        Assert.False(TournamentSpectatorOrange.ShouldRefill(orange, orange, 1));
        Assert.False(TournamentSpectatorOrange.ShouldRefill(other, orange, 0));
        Assert.False(TournamentSpectatorOrange.ShouldRefill(null, orange, 0));
    }
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void OrangePickupRules_BlockSpectatorsAndOranges(
        bool isSpectator,
        bool isOrange,
        bool expected)
    {
        Assert.Equal(expected, TournamentSpectatorOrange.ShouldBlockPickup(isSpectator, isOrange));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void OrangeDropRules_BlockSpectatorsOnly(bool isSpectator, bool expected)
    {
        Assert.Equal(expected, TournamentSpectatorOrange.ShouldBlockDrop(isSpectator));
    }
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void OrangeCollisionRules_OnlyHideSpectatorOranges(
        bool isSpectator,
        bool isOrange,
        bool expected)
    {
        Assert.Equal(expected, TournamentSpectatorOrange.ShouldDisappearOnCollision(isSpectator, isOrange));
    }
    private static TournamentSessionSnapshot Snapshot(TournamentSessionPhase phase)
    {
        TournamentContestantData[] contestants =
        {
            Contestant("fighter-slot", "fighter", false),
            Contestant("waiting-slot", "waiting", false),
            Contestant("eliminated-slot", "eliminated", false),
            Contestant("replaced-slot", "replaced", true)
        };
        var match = new TournamentMatchData(
            "match",
            "round",
            1,
            1,
            1,
            new[]
            {
                new TournamentTeamData(
                    "team",
                    new[] { "fighter-slot" },
                    0,
                    false,
                    1,
                    null)
            },
            Array.Empty<string>());

        return new TournamentSessionSnapshot(
            "session",
            "mission",
            "town",
            TournamentSpectatorSceneLayouts.EmpireArenaScene,
            "prize",
            phase,
            1,
            1,
            "match",
            "fighter",
            new[] { "waiting", "eliminated", "replaced", "spectator" },
            contestants,
            new[] { "spectator", "not-an-entrant" },
            Array.Empty<TournamentPlayerChoiceData>(),
            new[] { new TournamentRoundData("round", 0, 0, new[] { match }) },
            0,
            0,
            0,
            false,
            false,
            null);
    }

    private static TournamentContestantData Contestant(
        string slotId,
        string controllerId,
        bool isReplaced)
        => new(
            slotId,
            $"{controllerId}-character",
            1,
            controllerId,
            controllerId,
            true,
            isReplaced,
            false,
            null);

    private static string BarrierTransform(TournamentSpectatorBarrierData data)
        => FormattableString.Invariant(
            $"{data.PrefabName}:{data.Position.x:F3},{data.Position.y:F3},{data.Position.z:F3},{data.Rotation:F3},{data.Scale.x:F3},{data.Scale.y:F3},{data.Scale.z:F3}");
    private static string SpawnTransform(TournamentSpectatorSpawnData data)
        => FormattableString.Invariant(
            $"{data.SpawnId}:{data.Position.x:F3},{data.Position.y:F3},{data.Position.z:F3},{data.Rotation:F3}");
    private static bool SamePosition(Vec3 left, Vec3 right)
        => (left - right).LengthSquared < 0.0001f;
}
