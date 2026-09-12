using Common.Util;
using E2E.Tests.Environment;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

public class PlayerArmyWaitBehaviorTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }

    public PlayerArmyWaitBehaviorTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ClientArmyWaitMenuTick_PartialArmyRelationship_DoesNotRunUnsafeVanillaTick()
    {
        var client = TestEnvironment.Clients.First();

        client.Call(() =>
        {
            var mainParty = ObjectHelper.SkipConstructor<MobileParty>();
            var army = ObjectHelper.SkipConstructor<Army>();
            mainParty._army = army;
            Campaign.Current.MainParty = mainParty;

            var behavior = ObjectHelper.SkipConstructor<PlayerArmyWaitBehavior>();
            var method = AccessTools.Method(typeof(PlayerArmyWaitBehavior), "ArmyWaitMenuTick");
            Assert.NotNull(method);

            var exception = Record.Exception(() =>
                method.Invoke(behavior, new object[] { null, default(CampaignTime) }));

            Assert.Null(UnwrapInvocationException(exception));
        });
    }

    private static Exception? UnwrapInvocationException(Exception? exception)
        => exception is TargetInvocationException invocationException
            ? invocationException.InnerException
            : exception;
}
