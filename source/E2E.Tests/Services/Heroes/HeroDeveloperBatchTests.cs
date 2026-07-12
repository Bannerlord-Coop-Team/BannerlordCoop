using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.HeroDevelopers.Messages;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using Xunit.Abstractions;
using static GameInterface.Services.ObjectManager.ObjectManager;
using MathF = TaleWorlds.Library.MathF;

namespace E2E.Tests.Services.Heroes;

/// <summary>
/// Verifies ordered batching for the mutations produced by hero skill XP gains.
/// </summary>
public class HeroDeveloperBatchTests : SyncTestBase
{
    private readonly string heroId;
    private readonly string skillId = "test_one_handed";

    public HeroDeveloperBatchTests(ITestOutputHelper output) : base(output)
    {
        heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        foreach (EnvironmentInstance instance in AllInstances())
        {
            EnsureHeroDevelopmentModels(instance);
            EnsureHeroDevelopmentState(instance);
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.AddExisting(skillId, DefaultSkills.OneHanded));
            });
        }
    }

    [Fact]
    public void AddSkillXp_HarmonyCapture_UsesOneRoundTrippedBatch()
    {
        EnvironmentInstance sender = Clients.First();
        Dictionary<EnvironmentInstance, int> initialTotalXp = AllInstances()
            .ToDictionary(instance => instance, GetTotalXp);

        ClearNetworkMessages();

        sender.Call(() =>
        {
            Assert.True(sender.ObjectManager.TryGetObject(heroId, out Hero hero));
            hero.HeroDeveloper.AddSkillXp(
                DefaultSkills.OneHanded,
                1f,
                isAffectedByFocusFactor: true,
                shouldNotify: false);
        });

        NetworkHeroDeveloperBatchServer captured = Assert.Single(
            sender.NetworkSentMessages.GetMessages<NetworkHeroDeveloperBatchServer>());
        Assert.Single(sender.NetworkSentMessages);
        Assert.Single(Server.NetworkSentMessages);
        NetworkHeroDeveloperOperationType[] capturedTypes = captured.Operations
            .Select(operation => operation.Type)
            .ToArray();
        Assert.InRange(capturedTypes.Length, 2, 3);
        Assert.Equal(NetworkHeroDeveloperOperationType.RawXpGain, capturedTypes[0]);
        Assert.Equal(NetworkHeroDeveloperOperationType.SkillXpSet, capturedTypes[1]);
        if (capturedTypes.Length == 3)
        {
            Assert.Equal(NetworkHeroDeveloperOperationType.SkillLevelChange, capturedTypes[2]);
        }

        NetworkHeroDeveloperBatchServer roundTripped = sender.EnsureSerializable(captured);
        NetworkHeroDeveloperOperation rawXp = Assert.Single(
            roundTripped.Operations,
            operation => operation.Type == NetworkHeroDeveloperOperationType.RawXpGain);
        NetworkHeroDeveloperOperation skillXp = Assert.Single(
            roundTripped.Operations,
            operation => operation.Type == NetworkHeroDeveloperOperationType.SkillXpSet);

        ClearNetworkMessages();
        Server.SimulateMessage(sender.NetPeer, roundTripped);

        NetworkHeroDeveloperBatchClients response = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkHeroDeveloperBatchClients>());
        Assert.Single(Server.NetworkSentMessages);
        Assert.Equal(
            roundTripped.Operations.Select(operation => operation.Type),
            response.Operations.Select(operation => operation.Type));

        foreach (EnvironmentInstance peer in AllInstances())
        {
            AssertDeveloperState(
                peer,
                initialTotalXp[peer] + (2 * MathF.Round(rawXp.Value)),
                skillXp.Value);
        }
    }

    [Fact]
    public void Server_Batch_ReplaysStateMutationsInOrder()
    {
        EnvironmentInstance sender = Clients.First();
        Dictionary<EnvironmentInstance, int> initialTotalXp = AllInstances()
            .ToDictionary(instance => instance, GetTotalXp);
        string compactHeroId = Compact(heroId, typeof(Hero));
        string compactSkillId = Compact(skillId, typeof(SkillObject));
        var operations = new List<NetworkHeroDeveloperOperation>
        {
            new(NetworkHeroDeveloperOperationType.RawXpGain, null, 0.6f, 0, false),
            new(NetworkHeroDeveloperOperationType.SkillXpSet, compactSkillId, 12.5f, 0, false),
            new(NetworkHeroDeveloperOperationType.SkillXpSet, compactSkillId, 123.5f, 0, false),
        };
        var request = new NetworkHeroDeveloperBatchServer(compactHeroId, operations);

        ClearNetworkMessages();

        Server.EnsureSerializable(request);
        Server.SimulateMessage(sender.NetPeer, request);

        NetworkHeroDeveloperBatchClients response = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkHeroDeveloperBatchClients>());
        Assert.Single(Server.NetworkSentMessages);
        Assert.Equal(request.Operations.Select(operation => operation.Type),
            response.Operations.Select(operation => operation.Type));

        foreach (EnvironmentInstance peer in AllInstances())
        {
            AssertDeveloperState(
                peer,
                initialTotalXp[peer] + MathF.Round(0.6f),
                123.5f);
        }
    }

    [Fact]
    public void Server_Batch_ReplaysRawXpRoundingAndNotificationFlagsPerOperation()
    {
        EnvironmentInstance sender = Clients.First();
        Dictionary<EnvironmentInstance, int> initialTotalXp = AllInstances()
            .ToDictionary(instance => instance, GetTotalXp);
        string compactHeroId = Compact(heroId, typeof(Hero));
        var operations = new List<NetworkHeroDeveloperOperation>
        {
            new(NetworkHeroDeveloperOperationType.RawXpGain, null, 0.6f, 0, false),
            new(NetworkHeroDeveloperOperationType.RawXpGain, null, 0.6f, 0, true),
        };

        ClearNetworkMessages();

        var request = new NetworkHeroDeveloperBatchServer(compactHeroId, operations);
        Server.EnsureSerializable(request);
        Server.SimulateMessage(sender.NetPeer, request);

        NetworkHeroDeveloperBatchClients response = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkHeroDeveloperBatchClients>());
        Assert.Collection(
            response.Operations,
            operation =>
            {
                Assert.Equal(NetworkHeroDeveloperOperationType.RawXpGain, operation.Type);
                Assert.False(operation.ShouldNotify);
            },
            operation =>
            {
                Assert.Equal(NetworkHeroDeveloperOperationType.RawXpGain, operation.Type);
                Assert.True(operation.ShouldNotify);
            });

        foreach (EnvironmentInstance peer in AllInstances())
        {
            int expectedTotalXp = initialTotalXp[peer] + MathF.Round(0.6f) + MathF.Round(0.6f);
            Assert.Equal(expectedTotalXp, GetTotalXp(peer));
        }
    }

    [Fact]
    public void Server_RejectsOversizedAndMalformedBatchesBeforeBroadcastOrApply()
    {
        EnvironmentInstance sender = Clients.First();
        Dictionary<EnvironmentInstance, int> initialTotalXp = AllInstances()
            .ToDictionary(instance => instance, GetTotalXp);
        string compactHeroId = Compact(heroId, typeof(Hero));
        string compactSkillId = Compact(skillId, typeof(SkillObject));
        var oversizedOperations = Enumerable
            .Range(0, NetworkHeroDeveloperBatchServer.MaxOperationsPerMessage + 1)
            .Select(_ => new NetworkHeroDeveloperOperation(
                NetworkHeroDeveloperOperationType.RawXpGain,
                null,
                1f,
                0,
                false))
            .ToList();

        ClearNetworkMessages();
        NetworkHeroDeveloperBatchServer oversized = Server.EnsureSerializable(
            new NetworkHeroDeveloperBatchServer(compactHeroId, oversizedOperations));
        Server.SimulateMessage(sender.NetPeer, oversized);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkHeroDeveloperBatchClients>());
        AssertTotalXpUnchanged(initialTotalXp);

        ClearNetworkMessages();
        var malformedOperations = new List<NetworkHeroDeveloperOperation>
        {
            new(NetworkHeroDeveloperOperationType.RawXpGain, compactSkillId, 1f, 0, false),
        };
        NetworkHeroDeveloperBatchServer malformed = Server.EnsureSerializable(
            new NetworkHeroDeveloperBatchServer(compactHeroId, malformedOperations));
        Server.SimulateMessage(sender.NetPeer, malformed);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkHeroDeveloperBatchClients>());
        AssertTotalXpUnchanged(initialTotalXp);
    }

    [Fact]
    public void Client_RejectsOversizedAndMalformedBatchesBeforeApply()
    {
        EnvironmentInstance client = Clients.First();
        int initialTotalXp = GetTotalXp(client);
        string compactHeroId = Compact(heroId, typeof(Hero));
        string compactSkillId = Compact(skillId, typeof(SkillObject));
        var oversizedOperations = Enumerable
            .Range(0, NetworkHeroDeveloperBatchServer.MaxOperationsPerMessage + 1)
            .Select(_ => new NetworkHeroDeveloperOperation(
                NetworkHeroDeveloperOperationType.RawXpGain,
                null,
                1f,
                0,
                false))
            .ToList();
        var oversizedRequest = new NetworkHeroDeveloperBatchServer(compactHeroId, oversizedOperations);
        NetworkHeroDeveloperBatchClients oversized = client.EnsureSerializable(
            new NetworkHeroDeveloperBatchClients(oversizedRequest));

        client.SimulateMessage(Server.NetPeer, oversized);

        Assert.Equal(initialTotalXp, GetTotalXp(client));

        var malformedOperations = new List<NetworkHeroDeveloperOperation>
        {
            new(NetworkHeroDeveloperOperationType.RawXpGain, compactSkillId, 1f, 0, false),
        };
        var malformedRequest = new NetworkHeroDeveloperBatchServer(compactHeroId, malformedOperations);
        NetworkHeroDeveloperBatchClients malformed = client.EnsureSerializable(
            new NetworkHeroDeveloperBatchClients(malformedRequest));

        client.SimulateMessage(Server.NetPeer, malformed);

        Assert.Equal(initialTotalXp, GetTotalXp(client));
    }

    private int GetTotalXp(EnvironmentInstance instance)
    {
        int totalXp = 0;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject(heroId, out Hero hero));
            totalXp = hero.HeroDeveloper.TotalXp;
        });
        return totalXp;
    }

    private void AssertTotalXpUnchanged(Dictionary<EnvironmentInstance, int> expected)
    {
        foreach (EnvironmentInstance instance in AllInstances())
        {
            Assert.Equal(expected[instance], GetTotalXp(instance));
        }
    }

    private static void EnsureHeroDevelopmentModels(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            GameModels currentModels = Campaign.Current.Models;
            if (currentModels.CharacterDevelopmentModel != null &&
                currentModels.GenericXpModel != null)
            {
                return;
            }

            List<GameModel> models = currentModels.GetGameModels().ToList();
            if (currentModels.CharacterDevelopmentModel == null)
            {
                models.Add(new DefaultCharacterDevelopmentModel());
            }

            if (currentModels.GenericXpModel == null)
            {
                models.Add(new FixedGenericXpModel());
            }

            var gameModels = new GameModels(models);
            AccessTools.Field(typeof(Campaign), "_gameModels").SetValue(Campaign.Current, gameModels);
        });
    }

    private void EnsureHeroDevelopmentState(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject(heroId, out Hero hero));
            if (hero.CharacterAttributes == null)
            {
                hero._characterAttributes = new PropertyOwner<CharacterAttribute>();
            }
        });
    }

    /// <summary>
    /// Provides deterministic generic-XP scaling for batch synchronization tests.
    /// </summary>
    private sealed class FixedGenericXpModel : GenericXpModel
    {
        public override float GetXpMultiplier(Hero hero) => 1f;
    }

    private void AssertDeveloperState(
        EnvironmentInstance instance,
        int expectedTotalXp,
        float expectedSkillXp)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject(heroId, out Hero hero));
            Assert.Equal(expectedTotalXp, hero.HeroDeveloper.TotalXp);
            Assert.Equal(expectedSkillXp, hero.HeroDeveloper.GetSkillXp(DefaultSkills.OneHanded));
        });
    }

    private void ClearNetworkMessages()
    {
        Server.NetworkSentMessages.Clear();
        foreach (EnvironmentInstance client in Clients)
        {
            client.NetworkSentMessages.Clear();
        }
    }

    private IEnumerable<EnvironmentInstance> AllInstances() =>
        new[] { Server }.Concat(Clients);
}
