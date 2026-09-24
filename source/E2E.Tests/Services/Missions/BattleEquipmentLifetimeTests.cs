using Common.Logging;
using E2E.Tests.Environment;
using GameInterface.Registry.Auto;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Surrogates;
using ProtoBuf;
using System.IO;
using TaleWorlds.Localization;
using Missions.Battles;
using Missions.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class BattleEquipmentLifetimeTests : IDisposable
{
    private readonly E2ETestEnvironment testEnvironment;
    private readonly List<string> capturedLogs = new();
    private readonly object logGate = new();
    private readonly Action<string> captureLog;

    public BattleEquipmentLifetimeTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output);
        captureLog = CaptureLog;
        OutputSinkManager.AddLogCallback(captureLog);
    }

    public void Dispose()
    {
        OutputSinkManager.RemoveLogCallback(captureLog);
        testEnvironment.Dispose();
    }

    [Fact]
    public void TryDecode_WireEquipmentIsTransientForEverySpawnPurpose()
    {
        var server = testEnvironment.Server;
        var client = testEnvironment.Clients.First();
        string registeredEquipmentId = null!;
        Equipment serverEquipment = null!;

        server.Call(() =>
        {
            serverEquipment = new Equipment(Equipment.EquipmentType.Battle);
            Assert.True(server.ObjectManager.TryGetId(serverEquipment, out registeredEquipmentId));
        });

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Equipment>(
                registeredEquipmentId,
                out var registeredEquipment));
            Assert.True(client.ObjectManager.TryGetId(registeredEquipment, out var clientEquipmentId));
            Assert.Equal(registeredEquipmentId, clientEquipmentId);
        });

        ClearCapturedLogs();

        foreach (SpawnBatchPurpose purpose in Enum.GetValues(typeof(SpawnBatchPurpose)))
        {
            NetworkSpawnBattleAgents wire = null!;
            server.Call(() =>
            {
                var record = new BattleAgentSpawnData(
                    Guid.NewGuid(),
                    "imperial_infantry",
                    new Vec3(1f, 2f, 0f),
                    BattleSideEnum.Attacker,
                    100f,
                    "owner",
                    "map_event_party",
                    1,
                    serverEquipment,
                    default,
                    missionEquipmentData: null);
                NetworkSpawnBattleAgents encoded = new BattleAgentSpawnBatchCodec()
                    .Encode(new[] { record }, purpose)
                    .Single();
                wire = ProtoBuf.Serializer.DeepClone(encoded);
            });

            client.Call(() =>
            {
                Assert.True(new BattleAgentSpawnBatchCodec().TryDecode(
                    wire,
                    out BattleAgentSpawnData[] decoded));
                Equipment transientEquipment = Assert.Single(decoded).SpawnEquipment;
                Assert.NotNull(transientEquipment);
                Assert.False(client.ObjectManager.TryGetId(transientEquipment, out _));
            });
        }

        Assert.DoesNotContain(GetCapturedLogs(), ContainsClientEquipmentDiagnostic);

        ClearCapturedLogs();
        client.Call(() =>
        {
            var unsupportedEquipment = new Equipment(Equipment.EquipmentType.Battle);
            Assert.False(client.ObjectManager.TryGetId(unsupportedEquipment, out _));
        });

        Assert.Contains(GetCapturedLogs(), ContainsClientEquipmentLifetimeError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HeroEquipmentWireCopies_RemainTransientAndPreserveRegisteredEquipment(bool decodeOnServer)
    {
        _ = new SurrogateCollection();
        var server = testEnvironment.Server;
        var receiver = decodeOnServer ? server : testEnvironment.Clients.First();
        string battleId = null!;
        string civilianId = null!;
        byte[] wire = null!;
        server.Call(() =>
        {
            var battle = new Equipment(Equipment.EquipmentType.Battle);
            var civilian = new Equipment(Equipment.EquipmentType.Civilian);
            Assert.True(server.ObjectManager.TryGetId(battle, out battleId));
            Assert.True(server.ObjectManager.TryGetId(civilian, out civilianId));
            using var stream = new MemoryStream();
            Serializer.Serialize(stream, new NetworkInitializeNewHero(
                "Hero_wire_test", new TextObject("first"), new TextObject("name"),
                civilianId, battleId, civilian, battle));
            wire = stream.ToArray();
        });

        ClearCapturedLogs();
        receiver.Call(() =>
        {
            using var stream = new MemoryStream(wire);
            var received = Serializer.Deserialize<NetworkInitializeNewHero>(stream);
            Assert.Equal(Equipment.EquipmentType.Civilian, received.CivilianEquipment._equipmentType);
            Assert.Equal(Equipment.EquipmentType.Battle, received.BattleEquipment._equipmentType);
            Assert.Equal(Equipment.EquipmentSlotLength, received.CivilianEquipment._itemSlots.Length);
            Assert.Equal(Equipment.EquipmentSlotLength, received.BattleEquipment._itemSlots.Length);
            Assert.False(receiver.ObjectManager.Contains(received.CivilianEquipment));
            Assert.False(receiver.ObjectManager.Contains(received.BattleEquipment));
            Assert.True(receiver.ObjectManager.TryGetObject<Equipment>(battleId, out var registeredBattle));
            Assert.True(receiver.ObjectManager.TryGetObject<Equipment>(civilianId, out var registeredCivilian));
            Assert.NotSame(registeredBattle, received.BattleEquipment);
            Assert.NotSame(registeredCivilian, received.CivilianEquipment);
            Assert.False(TransientEquipmentSyncScope.IsActive);
        });
        Assert.DoesNotContain(GetCapturedLogs(), ContainsClientEquipmentDiagnostic);
    }

    [Fact]
    public void TransientEquipmentScope_SuppressesOnlyItsScopedClientEquipmentDiagnostic()
    {
        var client = testEnvironment.Clients.First();

        ClearCapturedLogs();
        client.Call(() =>
        {
            using (new TransientEquipmentSyncScope())
            {
                var fixtureEquipment = new Equipment(Equipment.EquipmentType.Battle);
                Assert.False(client.ObjectManager.TryGetId(fixtureEquipment, out _));
            }
        });
        Assert.DoesNotContain(GetCapturedLogs(), ContainsClientEquipmentDiagnostic);

        ClearCapturedLogs();
        client.Call(() =>
        {
            var ordinaryEquipment = new Equipment(Equipment.EquipmentType.Battle);
            Assert.False(client.ObjectManager.TryGetId(ordinaryEquipment, out _));
        });
        Assert.Contains(GetCapturedLogs(), ContainsClientEquipmentLifetimeError);
    }

    private void CaptureLog(string message)
    {
        lock (logGate)
        {
            capturedLogs.Add(message);
        }
    }

    private void ClearCapturedLogs()
    {
        lock (logGate)
        {
            capturedLogs.Clear();
        }
    }

    private string[] GetCapturedLogs()
    {
        lock (logGate)
        {
            return capturedLogs.ToArray();
        }
    }

    private static bool ContainsClientEquipmentLifetimeError(string message) =>
        message.Contains("Client created managed") &&
        message.Contains("TaleWorlds.Core.Equipment");

    private static bool ContainsClientEquipmentDiagnostic(string message) =>
        ContainsClientEquipmentLifetimeError(message) ||
        (message.Contains("Client updated managed") && message.Contains("_equipmentType"));
}
