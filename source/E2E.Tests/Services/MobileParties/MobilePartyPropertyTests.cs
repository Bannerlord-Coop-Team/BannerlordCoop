using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using static TaleWorlds.CampaignSystem.Party.MobileParty;

namespace E2E.Tests.Services.MobileParties;
public class MobilePartyPropertyTests : IDisposable
{
    E2ETestEnvironment TestEnvironement { get; }

    EnvironmentInstance Server => TestEnvironement.Server;
    IEnumerable<EnvironmentInstance> Clients => TestEnvironement.Clients;

    string PartyId { get; set; }
    string PartyId2 { get; set; }
    string HeroId { get; set; }

    public MobilePartyPropertyTests(ITestOutputHelper output)
    {
        TestEnvironement = new E2ETestEnvironment(output);


        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var party2 = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var hero = GameObjectCreator.CreateInitializedObject<Hero>();

            PartyId = party.StringId;
            HeroId = hero.StringId;

            party.CustomName = new TextObject("DefaultName");
            party.Engineer = hero;
            party.Scout = hero;
            party.Surgeon = hero;
            party.Quartermaster = hero;
            party.AttachedTo = party2;
            party.Ai = new MobilePartyAi(party);

            PartyId2 = party2.StringId;
        });

        Assert.NotNull(PartyId);
        Assert.NotNull(PartyId2);
        Assert.NotNull(HeroId);
    }

    public void Dispose()
    {
        TestEnvironement.Dispose();
    }

    [Fact]
    public void ServerChangeCustomName_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.CustomName = new TextObject("NewTestCustomName");
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.CustomName.Value, serverParty.CustomName.Value);
        }
    }

    [Fact]
    public void ClientChangeCustomName_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.CustomName = new TextObject("NewTestCustomName");
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.CustomName.Value, serverParty.CustomName.Value);
        }
    }

    [Fact]
    public void ServerChangeLastVisitedSettlement_SyncAllClients()
    {
        // Arrange
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        Server.ObjectManager.AddNewObject(settlement, out var settlementId);

        foreach(var client in TestEnvironement.Clients)
        {
            var newSettlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            client.ObjectManager.AddExisting(settlementId, newSettlement);
        }
        

        // Act
        Server.Call(() =>
        {
            serverParty.LastVisitedSettlement = settlement;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.LastVisitedSettlement.StringId, serverParty.LastVisitedSettlement.StringId);
        }
    }

    [Fact]
    public void ClientLastVisitedSettlement_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.LastVisitedSettlement = null;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.LastVisitedSettlement, serverParty.LastVisitedSettlement);
        }
    }

    [Fact]
    public void ServerChangeAggressiveness_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.Aggressiveness = 2f;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.Aggressiveness, serverParty.Aggressiveness);
        }
    }

    [Fact]
    public void ClientAggressiveness_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.Aggressiveness = 2f;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.Aggressiveness, serverParty.Aggressiveness);
        }
    }

    [Fact]
    public void ServerChangeObjective_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.Objective = PartyObjective.Aggressive;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.Objective, serverParty.Objective);
        }
    }

    [Fact]
    public void ClientObjective_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.Objective = PartyObjective.Defensive;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.Objective, serverParty.Objective);
        }
    }

    [Fact]
    public void ServerChangeAi_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId2, out var extraParty));

        // Act
        Server.Call(() =>
        {
            serverParty.Ai = extraParty.Ai;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.Ai._mobileParty.StringId, serverParty.Ai._mobileParty.StringId);
        }
    }

    [Fact]
    public void ClientAi_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.Ai = null;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.Ai._mobileParty.StringId, serverParty.Ai._mobileParty.StringId);
        }
    }

    [Fact]
    public void ServerChangeIsActive_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.IsActive = !serverParty.IsActive;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsActive, serverParty.IsActive);
        }
    }

    [Fact]
    public void ClientIsActive_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.IsActive = !serverParty.IsActive;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsActive, serverParty.IsActive);
        }
    }

    [Fact]
    public void ServerChangeIsPartyTradeActive_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.IsPartyTradeActive = !serverParty.IsActive;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsPartyTradeActive, serverParty.IsPartyTradeActive);
        }
    }

    [Fact]
    public void ClientIsPartyTradeActive_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.IsPartyTradeActive = !serverParty.IsActive;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsPartyTradeActive, serverParty.IsPartyTradeActive);
        }
    }

    [Fact]
    public void ServerChangePartyTradeGold_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.PartyTradeGold = 55;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.PartyTradeGold, serverParty.PartyTradeGold);
        }
    }

    [Fact]
    public void ClientPartyTradeGold_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.PartyTradeGold = 12;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.PartyTradeGold, serverParty.PartyTradeGold);
        }
    }

    [Fact]
    public void ServerChangePartyTradeTaxGold_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.PartyTradeTaxGold = 65;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.PartyTradeTaxGold, serverParty.PartyTradeTaxGold);
        }
    }

    [Fact]
    public void ClientPartyTradeTaxGold_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.PartyTradeTaxGold = 14;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.PartyTradeTaxGold, serverParty.PartyTradeTaxGold);
        }
    }

    [Fact]
    public void ServerChangeVersionNo_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.VersionNo = 3;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.VersionNo, serverParty.VersionNo);
        }
    }

    [Fact]
    public void ClientVersionNo_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.VersionNo = 5;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.VersionNo, serverParty.VersionNo);
        }
    }

    [Fact]
    public void ServerChangeShouldJoinPlayerBattles_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.ShouldJoinPlayerBattles = !serverParty.ShouldJoinPlayerBattles;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.ShouldJoinPlayerBattles, serverParty.ShouldJoinPlayerBattles);
        }
    }

    [Fact]
    public void ClientShouldJoinPlayerBattles_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.ShouldJoinPlayerBattles = !serverParty.ShouldJoinPlayerBattles;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.ShouldJoinPlayerBattles, serverParty.ShouldJoinPlayerBattles);
        }
    }

    [Fact]
    public void ServerChangeIsDisbanding_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.IsDisbanding = !serverParty.IsDisbanding;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsDisbanding, serverParty.IsDisbanding);
        }
    }

    [Fact]
    public void ClientIsDisbanding_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.IsDisbanding = !serverParty.IsDisbanding;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsDisbanding, serverParty.IsDisbanding);
        }
    }

    [Fact]
    public void ServerChangeCurrentSettlement_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        string? settlementId = null;
        Server.Call(() =>
        {
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            serverParty.CurrentSettlement = settlement;

            settlementId = settlement.StringId;
        });

        Assert.NotNull(settlementId);


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(settlementId, clientParty.CurrentSettlement.StringId);
        }
    }

    [Fact]
    public void ClientCurrentSettlement_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.CurrentSettlement = settlement;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.CurrentSettlement, serverParty.CurrentSettlement);
        }
    }

    [Fact]
    public void ServerChangeAttachedTo_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId2, out var extraParty));

        // Act
        Server.Call(() =>
        {
            serverParty.AttachedTo = extraParty;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.AttachedTo.StringId, serverParty.AttachedTo.StringId);
        }
    }

    [Fact]
    public void ClientAttachedTo_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.AttachedTo = null;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.AttachedTo.StringId, serverParty.AttachedTo.StringId);
        }
    }

    [Fact]
    public void ServerChangeBesiegerCamp_SyncAllClients()
    {
        // Arrange
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        string? besiegerCampId = null;
        string? partyId = null;
        Server.Call(() =>
        {
            var serverParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var besiegerCamp = GameObjectCreator.CreateInitializedObject<BesiegerCamp>();
            serverParty.BesiegerCamp = besiegerCamp;

            Server.ObjectManager.TryGetId(besiegerCamp, out besiegerCampId);
            Server.ObjectManager.TryGetId(serverParty, out partyId);


        }, new MethodBase[] { 
            AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.SetSiegeCampPartyPosition)),
            AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)),
        });

        Assert.NotNull(besiegerCampId);
        Assert.NotNull(partyId);


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var clientParty));
            Assert.True(client.ObjectManager.TryGetId(clientParty.BesiegerCamp, out var clientBesiegerCampId));
            Assert.Equal(besiegerCampId, clientBesiegerCampId);
        }
    }

    [Fact]
    public void ClientBesiegerCamp_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.BesiegerCamp = null;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.BesiegerCamp, serverParty.BesiegerCamp);
        }
    }

    [Fact]
    public void ServerChangeScout_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            var newScout = GameObjectCreator.CreateInitializedObject<Hero>();
            serverParty.Scout = newScout;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));

            // Assert hero changes
            Assert.NotEqual(HeroId, serverParty.Scout.StringId);
            Assert.Equal(clientParty.Scout.StringId, serverParty.Scout.StringId);
        }
    }

    [Fact]
    public void ClientScout_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();

        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));

            // Null because creating a hero causes issues
            serverParty.Scout = null;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));

            // Assert hero does not actually change
            Assert.Equal(HeroId, serverParty.Scout.StringId);
            Assert.Equal(clientParty.Scout.StringId, serverParty.Scout.StringId);
        }
    }

    [Fact]
    public void ServerChangeEngineer_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            var newEngineer = GameObjectCreator.CreateInitializedObject<Hero>();
            serverParty.Engineer = newEngineer;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));

            // Assert hero changes
            Assert.NotEqual(HeroId, serverParty.Engineer.StringId);
            Assert.Equal(clientParty.Engineer.StringId, serverParty.Engineer.StringId);
        }
    }

    [Fact]
    public void ClientEngineer_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();

        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));

            // Null because creating a hero causes issues
            serverParty.Engineer = null;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));

            // Assert hero does not actually change
            Assert.Equal(HeroId, serverParty.Engineer.StringId);
            Assert.Equal(clientParty.Engineer.StringId, serverParty.Engineer.StringId);
        }
    }

    [Fact]
    public void ServerChangeQuartermaster_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            var newQuartermaster = GameObjectCreator.CreateInitializedObject<Hero>();
            serverParty.Quartermaster = newQuartermaster;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));

            // Assert hero changes
            Assert.NotEqual(HeroId, serverParty.Quartermaster.StringId);
            Assert.Equal(clientParty.Quartermaster.StringId, serverParty.Quartermaster.StringId);
        }
    }

    [Fact]
    public void ClientQuartermaster_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();

        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));

            // Null because creating a hero causes issues
            serverParty.Quartermaster = null;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));

            // Assert hero does not actually change
            Assert.Equal(HeroId, serverParty.Quartermaster.StringId);
            Assert.Equal(clientParty.Quartermaster.StringId, serverParty.Quartermaster.StringId);
        }
    }

    [Fact]
    public void ServerChangeSurgeon_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            var newSurgeon = GameObjectCreator.CreateInitializedObject<Hero>();
            serverParty.Surgeon = newSurgeon;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));

            // Assert hero changes
            Assert.NotEqual(HeroId, serverParty.Surgeon.StringId);
            Assert.Equal(clientParty.Surgeon.StringId, serverParty.Surgeon.StringId);
        }
    }

    [Fact]
    public void ClientSurgeon_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();

        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));

            // Null because creating a hero causes issues
            serverParty.Surgeon = null;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));

            // Assert hero does not actually change
            Assert.Equal(HeroId, serverParty.Surgeon.StringId);
            Assert.Equal(clientParty.Surgeon.StringId, serverParty.Surgeon.StringId);
        }
    }

    [Fact]
    public void ServerChangeActualClan_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        var clan = GameObjectCreator.CreateInitializedObject<Clan>();
        Server.ObjectManager.AddNewObject(clan, out string clanId);

        foreach (var client in TestEnvironement.Clients)
        {
            var newClan = GameObjectCreator.CreateInitializedObject<Clan>();
            client.ObjectManager.AddExisting(clanId, newClan);
        }

        // Act
        Server.Call(() =>
        {
            serverParty.ActualClan = clan;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(serverParty.ActualClan.StringId, clientParty.ActualClan.StringId);
        }
    }

    [Fact]
    public void ClientActualClan_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        var clan = GameObjectCreator.CreateInitializedObject<Clan>();
        Server.ObjectManager.AddNewObject(clan, out string clanId);

        foreach (var client in TestEnvironement.Clients)
        {
            var newClan = GameObjectCreator.CreateInitializedObject<Clan>();
            client.ObjectManager.AddExisting(clanId, newClan);
        }

        Server.Call(() =>
        {
            serverParty.ActualClan = clan;
        });

        var firstClient = Clients.First();

        firstClient.Call(() =>
        {
            serverParty.ActualClan = null;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(serverParty.ActualClan.StringId, clientParty.ActualClan.StringId);
        }
    }

    [Fact]
    public void ServerChangeRecentEventsMorale_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.RecentEventsMorale = 3f;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.RecentEventsMorale, serverParty.RecentEventsMorale);
        }
    }

    [Fact]
    public void ClientRecentEventsMorale_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.RecentEventsMorale = 6f;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.RecentEventsMorale, serverParty.RecentEventsMorale);
        }
    }

    [Fact]
    public void ServerChangeEventPositionAdder_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.EventPositionAdder = new Vec2(5f, 4f);
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.EventPositionAdder, serverParty.EventPositionAdder);
        }
    }

    [Fact]
    public void ClientEventPositionAdder_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.EventPositionAdder = new Vec2(1f, 4f);
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.EventPositionAdder, serverParty.EventPositionAdder);
        }
    }

    [Fact]
    public void ServerChangeIsMilitia_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.IsMilitia = !serverParty.IsMilitia;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsMilitia, serverParty.IsMilitia);
        }
    }

    [Fact]
    public void ClientIsMilitia_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.IsMilitia = !serverParty.IsMilitia;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsMilitia, serverParty.IsMilitia);
        }
    }

    [Fact]
    public void ServerChangeIsLordParty_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.IsLordParty = !serverParty.IsLordParty;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsLordParty, serverParty.IsLordParty);
        }
    }

    [Fact]
    public void ClientIsLordParty_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.IsLordParty = !serverParty.IsLordParty;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsLordParty, serverParty.IsLordParty);
        }
    }

    [Fact]
    public void ServerChangeIsVillager_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.IsVillager = !serverParty.IsVillager;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsVillager, serverParty.IsVillager);
        }
    }

    [Fact]
    public void ClientIsVillager_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.IsVillager = !serverParty.IsVillager;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsVillager, serverParty.IsVillager);
        }
    }

    [Fact]
    public void ServerChangeIsCaravan_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.IsCaravan = !serverParty.IsCaravan;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsCaravan, serverParty.IsCaravan);
        }
    }

    [Fact]
    public void ClientIsCaravan_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.IsCaravan = !serverParty.IsCaravan;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsCaravan, serverParty.IsCaravan);
        }
    }

    [Fact]
    public void ServerChangeIsGarrison_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.IsGarrison = !serverParty.IsGarrison;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsGarrison, serverParty.IsGarrison);
        }
    }

    [Fact]
    public void ClientIsGarrison_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.IsGarrison = !serverParty.IsGarrison;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsGarrison, serverParty.IsGarrison);
        }
    }
    [Fact]
    public void ServerChangeIsCustomParty_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.IsCustomParty = !serverParty.IsCustomParty;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsCustomParty, serverParty.IsCustomParty);
        }
    }

    [Fact]
    public void ClientIsCustomParty_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.IsCustomParty = !serverParty.IsCustomParty;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsCustomParty, serverParty.IsCustomParty);
        }
    }
    [Fact]
    public void ServerChangeIsBandit_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.IsBandit = !serverParty.IsBandit;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsBandit, serverParty.IsBandit);
        }
    }

    [Fact]
    public void ClientIsBandit_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.IsBandit = !serverParty.IsBandit;
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.IsBandit, serverParty.IsBandit);
        }
    }
}