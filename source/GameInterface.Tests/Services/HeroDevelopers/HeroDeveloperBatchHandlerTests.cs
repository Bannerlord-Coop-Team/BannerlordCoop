using Common;
using Common.Messaging;
using Common.Network;
using Common.Tests.Utils;
using GameInterface.Services.HeroDevelopers.Handlers;
using GameInterface.Services.HeroDevelopers.Messages;
using GameInterface.Services.HeroDevelopers.Patches;
using GameInterface.Services.ObjectManager;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.HeroDevelopers;

/// <summary>
/// Verifies hero-developer batch chunking and isolated ordered replay.
/// </summary>
public class HeroDeveloperBatchHandlerTests
{
    static HeroDeveloperBatchHandlerTests()
    {
        // Ensure the shared game-loop pump is running when this class is tested in isolation.
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void LocalNestedBatch_AboveWireBound_IsSplitWithoutDroppingOperations()
    {
        var broker = new TestMessageBroker();
        var objectManager = new Mock<IObjectManager>();
        var network = new Mock<INetwork>();
        var sent = new List<IMessage>();
        var handler = new HeroDeveloperHandler(broker, objectManager.Object, network.Object);
        Hero hero = Uninitialized<Hero>();
        HeroDeveloper developer = Uninitialized<HeroDeveloper>();
        developer.Hero = hero;
        string heroId = "hero-test";
        objectManager.Setup(manager => manager.TryGetId(hero, out heroId)).Returns(true);
        network.Setup(instance => instance.SendAll(It.IsAny<IMessage>()))
            .Callback<IMessage>(message => sent.Add(message));

        HeroDeveloperBatchScope outer = HeroDeveloperBatchScope.Begin(developer);
        try
        {
            for (int index = 0; index <= NetworkHeroDeveloperBatchServer.MaxOperationsPerMessage; index++)
            {
                HeroDeveloperBatchScope inner = HeroDeveloperBatchScope.Begin(developer);
                Assert.True(HeroDeveloperBatchScope.TryEnqueue(new RawXpGain(developer, 1f, false)));
                Assert.Null(inner.Complete());
            }

            HeroDeveloperBatch batch = outer.Complete();
            Assert.NotNull(batch);

            broker.Publish(developer, batch);

            NetworkHeroDeveloperBatchServer[] requests = sent
                .OfType<NetworkHeroDeveloperBatchServer>()
                .ToArray();
            Assert.Equal(2, requests.Length);
            Assert.Equal(
                NetworkHeroDeveloperBatchServer.MaxOperationsPerMessage,
                requests[0].Operations.Count);
            Assert.Single(requests[1].Operations);
            Assert.Equal(
                NetworkHeroDeveloperBatchServer.MaxOperationsPerMessage + 1,
                requests.Sum(request => request.Operations.Count));
            Assert.All(
                requests.SelectMany(request => request.Operations),
                operation => Assert.Equal(NetworkHeroDeveloperOperationType.RawXpGain, operation.Type));
            Assert.DoesNotContain(sent, message => message is NetworkRawXpGainServer);
            Assert.DoesNotContain(sent, message => message is NetworkSetSkillXpServer);
            Assert.DoesNotContain(sent, message => message is NetworkSkillLevelChangeServer);
        }
        finally
        {
            outer.Abort();
            handler.Dispose();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NetworkBatch_WhenMiddleOperationThrows_AppliesLaterOperation(bool applyOnServer)
    {
        var broker = new TestMessageBroker();
        var objectManager = new Mock<IObjectManager>();
        var network = new Mock<INetwork>();
        var sent = new List<IMessage>();
        var handler = new HeroDeveloperHandler(broker, objectManager.Object, network.Object);
        Hero hero = Uninitialized<Hero>();
        HeroDeveloper developer = Uninitialized<HeroDeveloper>();
        SkillObject skill = Uninitialized<SkillObject>();
        developer.Hero = hero;
        developer._skillXps = new();
        hero._heroDeveloper = developer;

        Hero resolvedHero = hero;
        SkillObject resolvedSkill = skill;
        SkillObject failedSkill = null!;
        objectManager.Setup(manager => manager.TryGetObject("hero-test", out resolvedHero)).Returns(true);
        objectManager.Setup(manager => manager.TryGetObject("skill-test", out resolvedSkill)).Returns(true);
        objectManager.Setup(manager => manager.TryGetObject("skill-failure", out failedSkill))
            .Throws(new InvalidOperationException("expected test failure"));
        network.Setup(instance => instance.SendAll(It.IsAny<IMessage>()))
            .Callback<IMessage>(message => sent.Add(message));

        var request = new NetworkHeroDeveloperBatchServer(
            "hero-test",
            new List<NetworkHeroDeveloperOperation>
            {
                new(NetworkHeroDeveloperOperationType.SkillXpSet, "skill-test", 10f, 0, false),
                new(NetworkHeroDeveloperOperationType.SkillXpSet, "skill-failure", 20f, 0, false),
                new(NetworkHeroDeveloperOperationType.SkillXpSet, "skill-test", 30f, 0, false),
            });
        try
        {
            GameThread.Run(() =>
            {
                if (applyOnServer)
                {
                    broker.Publish(this, request);
                }
                else
                {
                    broker.Publish(this, new NetworkHeroDeveloperBatchClients(request));
                }
            }, blocking: true);

            if (applyOnServer)
            {
                Assert.Single(sent.OfType<NetworkHeroDeveloperBatchClients>());
            }
            else
            {
                Assert.Empty(sent);
            }

            Assert.Equal(30f, developer._skillXps[skill]);
        }
        finally
        {
            handler.Dispose();
        }
    }

    private static T Uninitialized<T>() where T : class =>
        (T)FormatterServices.GetUninitializedObject(typeof(T));
}
