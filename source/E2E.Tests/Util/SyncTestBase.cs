using E2E.Tests.Environment;
using Xunit.Abstractions;
using E2E.Tests.Environment.Instance;

namespace E2E.Tests.Util
{
    public abstract class SyncTestBase : IDisposable
    {
        internal E2ETestEnvironment TestEnvironment { get; }

        internal EnvironmentInstance Server => TestEnvironment.Server;

        internal IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        public SyncTestBase(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }
    }
}
