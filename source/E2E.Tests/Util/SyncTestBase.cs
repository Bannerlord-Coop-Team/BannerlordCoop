using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using Xunit.Abstractions;

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

        internal void AssertSingleAutoSyncMessageForPair(string retainedMessageName, string removedMessageName)
        {
            var sentMessage = Assert.Single(
                Server.NetworkSentMessages,
                message => message.GetType().Name == retainedMessageName || message.GetType().Name == removedMessageName);

            Assert.Equal(retainedMessageName, sentMessage.GetType().Name);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }
    }
}
