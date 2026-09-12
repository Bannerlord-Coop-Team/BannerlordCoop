using Common.Logging;
using E2E.Tests.Environment;
using GameInterface.Surrogates;
using HarmonyLib;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;
using static TaleWorlds.Core.Equipment;



namespace E2E.Tests.Services.Equipments;


public class EquipmentSyncTests : IDisposable
{
    private const string EquipmentTypeDiagnostic = "Client updated managed \"_equipmentType\"";

    E2ETestEnvironment TestEnvironment { get; }
    public EquipmentSyncTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateEquipment_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        string? EquipmentId = null;
        string? EquipmentWithEquipParamId = null;
        string? civilEquipmentId = null;


        // Act

        server.Call(() =>
        {
            // No Params
            var Equip = new Equipment();
            Assert.True(server.ObjectManager.TryGetId(Equip, out EquipmentId));

            // EquipmentType Param
            EquipmentType equipmentType = EquipmentType.Civilian;
            var civilEquip = new Equipment(equipmentType);
            string test2 = civilEquip.CalculateEquipmentCode();
            Assert.True(server.ObjectManager.TryGetId(civilEquip, out civilEquipmentId));

            // Equipment Param

            var EquipWithEquipParam = new Equipment(Equip);
            Assert.True(server.ObjectManager.TryGetId(EquipWithEquipParam, out EquipmentWithEquipParamId));

        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var Equip));

        Assert.True(server.ObjectManager.TryGetObject<Equipment>(civilEquipmentId, out var serverCivilEquipment));
        Assert.True(serverCivilEquipment.IsCivilian);

        Assert.True(server.ObjectManager.TryGetObject<Equipment>(EquipmentWithEquipParamId, out var serverEquipment));
        Assert.Equal(serverEquipment._equipmentType, Equip._equipmentType);


        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var _));
            Assert.True(client.ObjectManager.TryGetObject<Equipment>(civilEquipmentId, out var clientCivilEquipment));
            Assert.True(client.ObjectManager.TryGetObject<Equipment>(EquipmentWithEquipParamId, out var clientEquipment));

        }

    }


    [Fact]
    public void ClientCreateEquipment_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        string? EquipmentId = null;
        string? EquipmentWithEquipParamId = null;
        string? EquipmentWithExistingEquipId = null;
        string? civilEquipmentId = null;
        Equipment? ServerEquip = null;

        server.Call(() =>
        {
            ServerEquip = new Equipment();
            server.ObjectManager.TryGetId(ServerEquip, out EquipmentWithExistingEquipId); // for Equipment(Equipment equipment) test in client1.call
        });

        // Act


        client1.Call(() =>
        {
            Equipment Equip = new Equipment();
            Assert.False(client1.ObjectManager.TryGetId(Equip, out EquipmentId));

            // Equipment(bool IsCivil) 
            EquipmentType equipmentType = EquipmentType.Civilian;
            var civilEquip = new Equipment(equipmentType);
            Assert.False(client1.ObjectManager.TryGetId(civilEquip, out civilEquipmentId));

            // Equipment(Equipment equipment) 
            var EquipWithEquipParam = new Equipment(Equip);
            Assert.False(client1.ObjectManager.TryGetId(EquipWithEquipParam, out EquipmentWithEquipParamId));


            client1.ObjectManager.TryGetObject<Equipment>(EquipmentWithExistingEquipId, out var EquipParam);

            // For this test to pass requires working server-side syncing
            var EquipWithExistingEquip = new Equipment(EquipParam);
            Assert.False(client1.ObjectManager.TryGetId(EquipWithExistingEquip, out EquipmentWithExistingEquipId));

        });

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var _));
        Assert.False(server.ObjectManager.TryGetObject<Equipment>(civilEquipmentId, out var _));
        Assert.False(server.ObjectManager.TryGetObject<Equipment>(EquipmentWithEquipParamId, out var _));
        Assert.False(server.ObjectManager.TryGetObject<Equipment>(EquipmentWithExistingEquipId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var _));
            Assert.False(client.ObjectManager.TryGetObject<Equipment>(civilEquipmentId, out var _));
            Assert.False(client.ObjectManager.TryGetObject<Equipment>(EquipmentWithEquipParamId, out var _));
            Assert.False(client.ObjectManager.TryGetObject<Equipment>(EquipmentWithExistingEquipId, out var _));

        }
    }

    [Fact]
    public void Server_EquipmentType()
    {

        // Arrange
        var server = TestEnvironment.Server;

        string EquipmentId = null;

        var field = AccessTools.Field(typeof(Equipment), nameof(Equipment._equipmentType));


        // Get field intercept to use on the server to simulate the field changing
        var intercept = TestEnvironment.GetIntercept(field);


        Equipment.EquipmentType equipmentType = Equipment.EquipmentType.Civilian;

        // Act
        server.Call(() =>
        {
            var Equipment = new Equipment();

            Assert.True(server.ObjectManager.TryGetId(Equipment, out EquipmentId));

            Assert.True(server.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var equipment));

            Assert.NotEqual(equipment._equipmentType, equipmentType);

            // Simulate the field changing
            intercept.Invoke(null, new object[] { equipment, equipmentType });

            Assert.Equal(equipmentType, equipment._equipmentType);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Equipment>(EquipmentId, out var equipment));

            Assert.Equal(equipment._equipmentType, equipmentType);
        }

    }

    [Theory]
    [InlineData(SpawnBatchPurpose.Initial)]
    [InlineData(SpawnBatchPurpose.Deployment)]
    [InlineData(SpawnBatchPurpose.CatchUp)]
    public void ClientTransientSpawnEquipment_DoesNotReportManagedMutation(SpawnBatchPurpose purpose)
    {
        _ = new SurrogateCollection();
        var server = TestEnvironment.Server;
        var client = TestEnvironment.Clients.First();
        NetworkSpawnBattleAgents? wireMessage = null;

        server.Call(() =>
        {
            var codec = new BattleAgentSpawnBatchCodec();
            var record = new BattleAgentSpawnData(
                Guid.NewGuid(),
                "imperial_infantry",
                new Vec3(1f, 2f, 0f),
                BattleSideEnum.Attacker,
                100f,
                "owner",
                "map_event_party",
                1,
                new Equipment(EquipmentType.Civilian),
                default,
                missionEquipmentData: null);
            NetworkSpawnBattleAgents encoded = codec.Encode(
                new[] { record },
                purpose).Single();

            wireMessage = CreateWireMessage(encoded);
        });

        var diagnostics = new List<string>();
        void CaptureEquipmentTypeDiagnostic(string message)
        {
            if (message == EquipmentTypeDiagnostic)
                diagnostics.Add(message);
        }

        OutputSinkManager.AddLogCallback(CaptureEquipmentTypeDiagnostic);
        try
        {
            client.Call(() =>
            {
                var codec = new BattleAgentSpawnBatchCodec();
                Assert.True(codec.TryDecode(wireMessage!, out BattleAgentSpawnData[] decoded));

                Equipment equipment = Assert.Single(decoded).SpawnEquipment;
                Assert.Equal(EquipmentType.Civilian, equipment._equipmentType);
                Assert.False(client.ObjectManager.Contains(equipment));
            });
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(CaptureEquipmentTypeDiagnostic);
        }

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ClientRegisteredEquipmentMutation_ReportsManagedMutation()
    {
        var server = TestEnvironment.Server;
        var client = TestEnvironment.Clients.First();
        var field = AccessTools.Field(typeof(Equipment), nameof(Equipment._equipmentType));
        var intercept = TestEnvironment.GetIntercept(field);
        string? equipmentId = null;

        server.Call(() =>
        {
            var equipment = new Equipment();
            Assert.True(server.ObjectManager.TryGetId(equipment, out equipmentId));
        });

        var diagnostics = new List<string>();
        void CaptureEquipmentTypeDiagnostic(string message)
        {
            if (message == EquipmentTypeDiagnostic)
                diagnostics.Add(message);
        }

        OutputSinkManager.AddLogCallback(CaptureEquipmentTypeDiagnostic);
        try
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject(equipmentId, out Equipment equipment));
                Assert.True(client.ObjectManager.Contains(equipment));

                intercept.Invoke(null, new object[] { equipment, EquipmentType.Civilian });

                Assert.Equal(EquipmentType.Civilian, equipment._equipmentType);
            });
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(CaptureEquipmentTypeDiagnostic);
        }

        Assert.Single(diagnostics);
    }

    private static NetworkSpawnBattleAgents CreateWireMessage(NetworkSpawnBattleAgents encoded)
    {
        return new NetworkSpawnBattleAgents(
            encoded.Payload,
            encoded.UncompressedLength,
            encoded.RecordCount,
            encoded.IsCompressed,
            encoded.TransferId,
            encoded.BatchIndex,
            encoded.BatchCount,
            encoded.Purpose,
            encoded.PayloadSha256,
            agents: null!);
    }

}
