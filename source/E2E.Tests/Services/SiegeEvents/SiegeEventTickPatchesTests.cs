using Common.Util;
using E2E.Tests.Environment;
using GameInterface.Services.SiegeEvents.Patches;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Siege;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>
/// Covers <see cref="SiegeEventTickPatches"/>: the siege tick and siege-action funnel run only on the server,
/// otherwise a client builds unregistered ghost engines.
/// </summary>
public class SiegeEventTickPatchesTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    /// <summary>Builds the server and client instances the patches run against.</summary>
    public SiegeEventTickPatchesTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    /// <summary>Tears down the test environment.</summary>
    public void Dispose() => environment.Dispose();

    /// <summary>A client skips the vanilla tick, which would otherwise walk the uninitialized siege list.</summary>
    [Fact]
    public void Tick_SkipsVanillaOnClient()
    {
        var manager = ObjectHelper.SkipConstructor<SiegeEventManager>();

        environment.Clients.First().Call(() => manager.Tick(1f));
        environment.Server.Call(() => Assert.Throws<NullReferenceException>(() => manager.Tick(1f)));
    }

    /// <summary>A client skips the vanilla siege action, which would otherwise construct and deploy a ghost engine.</summary>
    [Fact]
    public void DoSiegeAction_SkipsVanillaOnClient()
    {
        var siegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();
        void DoAction() => siegeEvent.DoSiegeAction(null, SiegeStrategyActionModel.SiegeAction.ConstructNewSiegeEngine, null, 0, 0);

        environment.Clients.First().Call(DoAction);
        environment.Server.Call(() => Assert.Throws<NullReferenceException>(DoAction));
    }
}
