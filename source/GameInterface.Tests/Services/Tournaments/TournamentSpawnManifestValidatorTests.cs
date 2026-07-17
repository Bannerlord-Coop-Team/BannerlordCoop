using GameInterface.Services.Tournaments;
using GameInterface.Tests.Bootstrap;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Surrogates;
using ProtoBuf;
using System;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments;

public class TournamentSpawnManifestValidatorTests
{
    [Fact]
    public void TournamentAgentSpawnData_RoundTripsSerializableVectors()
    {
        GameBootStrap.Initialize();
        new SurrogateCollection();
        TournamentSessionSnapshot snapshot = CreateSnapshot();
        TournamentSpawnManifestData manifest = CreateManifest(snapshot, "player", "host");
        ItemObject equipmentItem = manifest.Agents[0].Equipment[0].Item;
        MBObjectManager.Instance.RegisterObject(equipmentItem);

        TournamentSpawnManifestData deserialized;
        using (var stream = new MemoryStream())
        {
            Serializer.Serialize(stream, manifest);
            stream.Position = 0;
            deserialized = Serializer.Deserialize<TournamentSpawnManifestData>(stream);
        }

        Assert.Equal(manifest.Agents[1].Position.x, deserialized.Agents[1].Position.x);
        Assert.Equal(manifest.Agents[1].Position.y, deserialized.Agents[1].Position.y);
        Assert.Equal(manifest.Agents[1].Position.z, deserialized.Agents[1].Position.z);
        Assert.Equal(manifest.Agents[1].Direction.X, deserialized.Agents[1].Direction.X);
        Assert.Equal(manifest.Agents[1].Direction.Y, deserialized.Agents[1].Direction.Y);
        Assert.Equal(manifest.Agents[1].Equipment.Length, deserialized.Agents[1].Equipment.Length);
        Assert.Equal(equipmentItem, deserialized.Agents[1].Equipment[0].Item);
    }

    [Fact]
    public void IsValid_EnforcesCanonicalHumanAndNpcOwnership()
    {
        TournamentSessionSnapshot snapshot = CreateSnapshot();
        TournamentSpawnManifestData valid = CreateManifest(snapshot, "player", "host");

        Assert.True(TournamentSpawnManifestValidator.IsValid(valid, snapshot));
        Assert.False(TournamentSpawnManifestValidator.IsValid(
            CreateManifest(snapshot, "host", "host"),
            snapshot));
        Assert.False(TournamentSpawnManifestValidator.IsValid(
            CreateManifest(snapshot, "player", "outsider"),
            snapshot));
    }

    [Fact]
    public void IsValid_RejectsDuplicateAgentIdentityAndWrongTeamBanner()
    {
        TournamentSessionSnapshot snapshot = CreateSnapshot();
        TournamentSpawnManifestData valid = CreateManifest(snapshot, "player", "host");
        TournamentAgentSpawnData first = valid.Agents[0];
        TournamentAgentSpawnData second = valid.Agents[1];
        valid.Agents[1] = new TournamentAgentSpawnData(
            first.AgentId,
            second.SlotId,
            second.CharacterId,
            second.DescriptorSeed,
            second.TeamId,
            second.TeamColor,
            "wrong-banner",
            second.ControllerId,
            second.Equipment,
            second.Position,
            second.Direction,
            second.Health,
            second.MountAgentId,
            second.MountCharacterId,
            second.MountDescriptorSeed,
            second.MountEquipment,
            second.MountHealth);

        Assert.False(TournamentSpawnManifestValidator.IsValid(valid, snapshot));
    }

    [Fact]
    public void IsValid_AllowsMountCharacterDerivedFromSynchronizedHorseEquipment()
    {
        TournamentSessionSnapshot snapshot = CreateSnapshot();
        TournamentSpawnManifestData manifest = CreateManifest(snapshot, "player", "host");
        TournamentAgentSpawnData npc = manifest.Agents[1];
        manifest.Agents[1] = new TournamentAgentSpawnData(
            npc.AgentId,
            npc.SlotId,
            npc.CharacterId,
            npc.DescriptorSeed,
            npc.TeamId,
            npc.TeamColor,
            npc.TeamBannerCode,
            npc.ControllerId,
            npc.Equipment,
            npc.Position,
            npc.Direction,
            npc.Health,
            Guid.NewGuid(),
            null,
            77,
            EquipmentAt(EquipmentIndex.Horse, new ItemObject("horse")),
            90);

        Assert.True(TournamentSpawnManifestValidator.IsValid(manifest, snapshot));
    }
    private static TournamentSessionSnapshot CreateSnapshot()
    {
        var contestants = new[]
        {
            new TournamentContestantData("human", "hero", 1, "player", "Player", true, false, true, null),
            new TournamentContestantData("npc", "troop", 2, null, "Troop", false, false, false, null)
        };
        var teams = new[]
        {
            new TournamentTeamData("team-a", new[] { "human" }, 0, false, 1, "banner-a"),
            new TournamentTeamData("team-b", new[] { "npc" }, 0, false, 2, "banner-b")
        };
        var match = new TournamentMatchData("match", "round", 0, 1, 1, teams, Array.Empty<string>(), 1);
        return new TournamentSessionSnapshot(
            "session", "mission", "town", "arena", "prize",
            TournamentSessionPhase.LiveMatch, 4, 2, "match", "host", Array.Empty<string>(),
            contestants, Array.Empty<string>(), Array.Empty<TournamentPlayerChoiceData>(),
            new[] { new TournamentRoundData("round", 0, 0, new[] { match }) },
            0, 0, 2, false, false, null);
    }

    private static EquipmentElement[] EquipmentAt(EquipmentIndex index, ItemObject item)
    {
        var equipment = new EquipmentElement[(int)EquipmentIndex.NumEquipmentSetSlots];
        equipment[(int)index] = new EquipmentElement(item);
        return equipment;
    }
    private static TournamentSpawnManifestData CreateManifest(
        TournamentSessionSnapshot snapshot,
        string humanOwner,
        string npcOwner)
    {
        TournamentTeamData[] teams = snapshot.Rounds[0].Matches[0].Teams;
        var equipment = EquipmentAt(
            EquipmentIndex.WeaponItemBeginSlot,
            new ItemObject($"weapon-{Guid.NewGuid()}"));
        var agents = new[]
        {
            new TournamentAgentSpawnData(
                Guid.NewGuid(), "human", "hero", 1, teams[0].TeamId, teams[0].TeamColor,
                teams[0].BannerCode, humanOwner, equipment, Vec3.Zero, new Vec2(0, 1), 100,
                Guid.Empty, null, 0, Array.Empty<EquipmentElement>(), 0),
            new TournamentAgentSpawnData(
                Guid.NewGuid(), "npc", "troop", 2, teams[1].TeamId, teams[1].TeamColor,
                teams[1].BannerCode, npcOwner, equipment, new Vec3(1, 0, 0), new Vec2(0, 1), 100,
                Guid.Empty, null, 0, Array.Empty<EquipmentElement>(), 0)
        };
        return new TournamentSpawnManifestData(
            snapshot.SessionId,
            snapshot.CurrentMatchId,
            snapshot.Revision,
            snapshot.BracketRevision,
            1,
            agents);
    }
}
