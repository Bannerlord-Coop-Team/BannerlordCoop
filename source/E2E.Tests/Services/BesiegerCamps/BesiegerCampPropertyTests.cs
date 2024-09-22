using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BesiegerCamps
{
    public class BesiegerCampPropertyTests : IDisposable
    {
        E2ETestEnvironment TestEnvironement { get; }

        EnvironmentInstance Server => TestEnvironement.Server;
        IEnumerable<EnvironmentInstance> Clients => TestEnvironement.Clients;

        string BesiegerCampId;

        public void Dispose()
        {
            TestEnvironement.Dispose();
        }

        private BesiegerCamp CreateCamp(out string beseigerCampId)
        {
            string? id = null;
            BesiegerCamp? beseigerCamp = null;
            Server.Call(() =>
            {
                beseigerCamp = GameObjectCreator.CreateInitializedObject<BesiegerCamp>();
                Assert.True(Server.ObjectManager.TryGetId(beseigerCamp, out id));
            }, new MethodBase[] {
            AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.SetSiegeCampPartyPosition)),
            AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)),
        });
            beseigerCampId = id!;
            return beseigerCamp!;
        }

        public BesiegerCampPropertyTests(ITestOutputHelper output)
        {
            TestEnvironement = new E2ETestEnvironment(output);

            CreateCamp(out BesiegerCampId);

            //Server.ObjectManager.AddNewObject(besiegerCamp, out string BesiegerCampId);

            foreach (var client in Clients)
            {
                var _besiegerCamp = new BesiegerCamp(null);

                client.ObjectManager.AddExisting(this.BesiegerCampId, _besiegerCamp);
            }
        }

        [Fact]
        public void ServerChangeBesiegerCampNumber_SyncAllClients()
        {
            Assert.True(Server.ObjectManager.TryGetObject<BesiegerCamp>(BesiegerCampId, out var serverBesiegerCamp));

            // Act
            Server.Call(() =>
            {
                serverBesiegerCamp.NumberOfTroopsKilledOnSide = new Random().Next(0, 10000);
            }, new MethodBase[] {
            AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.SetSiegeCampPartyPosition)),
            AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)),
        });


            // Assert
            foreach (var client in TestEnvironement.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(BesiegerCampId, out var clientBesiegerCamp));
                Assert.Equal(serverBesiegerCamp.NumberOfTroopsKilledOnSide, clientBesiegerCamp.NumberOfTroopsKilledOnSide);
            }
        }

        //[Fact]
        //public void ServerChangeBesiegerCampInitialCapital_SyncAllClients()
        //{
        //    Assert.True(Server.ObjectManager.TryGetObject<BesiegerCamp>(BesiegerCampId, out var serverBesiegerCamp));

        //    // Act
        //    Server.Call(() =>
        //    {
        //        serverBesiegerCamp.InitialCapital = 5;
        //    });


        //    // Assert
        //    foreach (var client in TestEnvironement.Clients)
        //    {
        //        Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(BesiegerCampId, out var clientBesiegerCamp));
        //        Assert.Equal(serverBesiegerCamp.InitialCapital, clientBesiegerCamp.InitialCapital);
        //    }
        //}

        //[Fact]
        //public void ServerChangeBesiegerCampLastRunCampaignTime_SyncAllClients()
        //{
        //    Assert.True(Server.ObjectManager.TryGetObject<BesiegerCamp>(BesiegerCampId, out var serverBesiegerCamp));

        //    // Act
        //    Server.Call(() =>
        //    {
        //        serverBesiegerCamp.LastRunCampaignTime = new CampaignTime(500);
        //    });

        //    // Assert
        //    foreach (var client in TestEnvironement.Clients)
        //    {
        //        Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(BesiegerCampId, out var clientBesiegerCamp));
        //        Assert.Equal(serverBesiegerCamp.LastRunCampaignTime, clientBesiegerCamp.LastRunCampaignTime);
        //    }
        //}

        //[Fact]
        //public void ServerChangeBesiegerCampType_SyncAllClients()
        //{
        //    Assert.True(Server.ObjectManager.TryGetObject<BesiegerCamp>(BesiegerCampId, out var serverBesiegerCamp));
        //    Assert.True(Server.ObjectManager.TryGetObject<BesiegerCampType>(BesiegerCampTypeId2, out var serverBesiegerCampType));
        //    // Act
        //    Server.Call(() =>
        //    {
        //        serverBesiegerCamp.BesiegerCampType = serverBesiegerCampType;
        //    });

        //    // Assert
        //    foreach (var client in TestEnvironement.Clients)
        //    {
        //        Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(BesiegerCampId, out var clientBesiegerCamp));
        //        Assert.Equal(serverBesiegerCamp.BesiegerCampType.StringId, clientBesiegerCamp.BesiegerCampType.StringId);
        //    }
        //}
    }
}
