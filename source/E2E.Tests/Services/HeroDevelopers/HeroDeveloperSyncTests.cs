using Common.Network.Coalescing;
using E2E.Tests.Util;
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
}
