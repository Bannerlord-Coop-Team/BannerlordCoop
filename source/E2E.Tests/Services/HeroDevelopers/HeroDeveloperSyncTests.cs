using Common.Network;
using Common.Network.Coalescing;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.CharacterDevelopers.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.HeroDevelopers;

public class HeroDeveloperSyncTests : SyncTestBase
{
    private string HeroDeveloperId;

    public HeroDeveloperSyncTests(ITestOutputHelper output) : base(output)
    {
        HeroDeveloperId = TestEnvironment.CreateRegisteredObject<HeroDeveloper>();
        TestEnvironment.CreateRegisteredObject<Hero>();
    }

    [Fact]
    public void Server_HeroDeveloper_Properties()
    {
        Server.ObjectManager.TryGetObject(HeroDeveloperId, out HeroDeveloper heroDeveloper);
        TestEnvironment.AssertProperty<HeroDeveloper, int>(nameof(HeroDeveloper.UnspentFocusPoints), 5);
        TestEnvironment.AssertProperty<HeroDeveloper, int>(nameof(HeroDeveloper.UnspentAttributePoints), 5);
        TestEnvironment.AssertReferenceProperty<HeroDeveloper, Hero>(nameof(HeroDeveloper.Hero));
    }

    [Fact]
    public void Server_HeroDeveloper_Fields()
    {
        var assertHelper = TestEnvironment.CreateAssertHelper<HeroDeveloper>(HeroDeveloperId);

        //TestEnvironment.AssertDictionaryField<HeroDeveloper, (PropertyObject, float)>(nameof(HeroDeveloper._skillXps));
        assertHelper.AssertPropertyOwnerField<HeroDeveloper, SkillObject>(nameof(HeroDeveloper._newFocuses));
    }

    [Fact]
    public void Server_TotalXpSets_CoalesceToLatestValue()
    {
        var totalXpField = AccessTools.Field(typeof(HeroDeveloper), nameof(HeroDeveloper._totalXp));
        var intercept = TestEnvironment.GetIntercept(totalXpField);
        int initialTotalXp = 0;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(HeroDeveloperId, out HeroDeveloper heroDeveloper));
            initialTotalXp = heroDeveloper._totalXp;

            intercept.Invoke(null, new object[] { heroDeveloper, 100 });
            intercept.Invoke(null, new object[] { heroDeveloper, 250 });
            intercept.Invoke(null, new object[] { heroDeveloper, 777 });

            Assert.Equal(777, heroDeveloper._totalXp);
        });

        Assert.True(Server.Resolve<ISendCoalescer>().HasPending);
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject(HeroDeveloperId, out HeroDeveloper heroDeveloper));
            Assert.Equal(initialTotalXp, heroDeveloper._totalXp);
        }

        TestEnvironment.FlushCoalescer();

        Assert.False(Server.Resolve<ISendCoalescer>().HasPending);
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject(HeroDeveloperId, out HeroDeveloper heroDeveloper));
            Assert.Equal(777, heroDeveloper._totalXp);
        }
    }

    [Fact]
    public void Server_HeroDeveloper_SetFocusValue_PropagatesToClients()
    {
        var skillObjectId = TestEnvironment.CreateRegisteredObject<SkillObject>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(HeroDeveloperId, out HeroDeveloper heroDeveloper));
            Assert.True(Server.ObjectManager.TryGetObject(skillObjectId, out SkillObject skill));

            heroDeveloper.SetFocus(skill, 3);

            Assert.Equal(3, heroDeveloper.GetFocus(skill));
        });

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject(HeroDeveloperId, out HeroDeveloper heroDeveloper));
            Assert.True(client.ObjectManager.TryGetObject(skillObjectId, out SkillObject skill));

            Assert.Equal(3, heroDeveloper.GetFocus(skill));
        }
    }

    [Fact]
    public void Client_StaleFocusTarget_DoesNotSpendServerPoints()
    {
        var skillId = TestEnvironment.CreateRegisteredObject<SkillObject>();
        SetServerFocusState(new[] { skillId }, focusLevel: 4, unspentFocusPoints: 1);

        SendFocusTargets(Clients.First(), new[] { skillId }, new[] { 4 });

        AssertFocusState(Server, skillId, focusLevel: 4, unspentFocusPoints: 1);
        foreach (var client in Clients)
        {
            AssertFocusState(client, skillId, focusLevel: 4, unspentFocusPoints: 1);
        }
    }

    [Fact]
    public void Client_FocusTarget_SpendsOnceAndPropagatesToClients()
    {
        var skillId = TestEnvironment.CreateRegisteredObject<SkillObject>();
        SetServerFocusState(new[] { skillId }, focusLevel: 3, unspentFocusPoints: 2);
        var client = Clients.First();

        SendFocusTargets(client, new[] { skillId }, new[] { 5 });
        SendFocusTargets(client, new[] { skillId }, new[] { 5 });

        AssertFocusState(Server, skillId, focusLevel: 5, unspentFocusPoints: 0);
        foreach (var currentClient in Clients)
        {
            AssertFocusState(currentClient, skillId, focusLevel: 5, unspentFocusPoints: 0);
        }
    }

    [Fact]
    public void Client_UnaffordableFocusTargets_ApplyNoChanges()
    {
        var firstSkillId = TestEnvironment.CreateRegisteredObject<SkillObject>();
        var secondSkillId = TestEnvironment.CreateRegisteredObject<SkillObject>();
        SetServerFocusState(new[] { firstSkillId, secondSkillId }, focusLevel: 1, unspentFocusPoints: 1);

        SendFocusTargets(Clients.First(), new[] { firstSkillId, secondSkillId }, new[] { 2, 2 });

        AssertFocusState(Server, firstSkillId, focusLevel: 1, unspentFocusPoints: 1);
        AssertFocusState(Server, secondSkillId, focusLevel: 1, unspentFocusPoints: 1);
        foreach (var client in Clients)
        {
            AssertFocusState(client, firstSkillId, focusLevel: 1, unspentFocusPoints: 1);
            AssertFocusState(client, secondSkillId, focusLevel: 1, unspentFocusPoints: 1);
        }
    }

    private void SetServerFocusState(IEnumerable<string> skillIds, int focusLevel, int unspentFocusPoints)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(HeroDeveloperId, out HeroDeveloper heroDeveloper));
            foreach (string skillId in skillIds)
            {
                Assert.True(Server.ObjectManager.TryGetObject(skillId, out SkillObject skill));
                heroDeveloper.SetFocus(skill, focusLevel);
            }

            heroDeveloper.UnspentFocusPoints = unspentFocusPoints;
        });
    }

    private void SendFocusTargets(EnvironmentInstance client, IEnumerable<string> skillIds, IEnumerable<int> focusLevels)
    {
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkApplyChanges(
            HeroDeveloperId,
            new List<string>(),
            new List<string>(),
            new List<int>(),
            skillIds.ToList(),
            focusLevels.ToList())));
    }

    private void AssertFocusState(EnvironmentInstance instance, string skillId, int focusLevel, int unspentFocusPoints)
    {
        Assert.True(instance.ObjectManager.TryGetObject(HeroDeveloperId, out HeroDeveloper heroDeveloper));
        Assert.True(instance.ObjectManager.TryGetObject(skillId, out SkillObject skill));
        Assert.Equal(focusLevel, heroDeveloper.GetFocus(skill));
        Assert.Equal(unspentFocusPoints, heroDeveloper.UnspentFocusPoints);
    }
}
