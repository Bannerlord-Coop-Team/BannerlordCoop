using TaleWorlds.CampaignSystem;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using Xunit.Abstractions;
using Common.Util;
using static Common.Extensions.ReflectionExtensions;

namespace E2E.Tests.Services.Kingdoms;

public class KingdomPropertyTests : IDisposable
{
    private readonly List<MethodBase> disabledMethods;
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;
    private IEnumerable<EnvironmentInstance> AllEnvironmentInstances => Clients.Append(Server);

    private readonly string kingdomId;

    public KingdomPropertyTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        disabledMethods = new List<MethodBase> {
            //Add your disabled methods
        };

        // Create Kingdom on the server
        kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>(disabledMethods);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }


}
