using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using GameInterface.Services.ObjectManager;
using Xunit.Sdk;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using static System.Net.Mime.MediaTypeNames;
using TaleWorlds.Library;

namespace E2E.Tests.Services.Equipments;

public class EquipmentDeleteTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public EquipmentDeleteTests(ITestOutputHelper output)
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
        Equipment? parameter = null;

        // Act

        server.Call(() =>
        {
          

        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {


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
            
        });

        // Act


        client1.Call(() =>
        {
            

        });

        // Assert
       

        foreach (var client in TestEnvironment.Clients)
        {
          

        }
    }

}