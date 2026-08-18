using GameInterface.Policies;
using System.Threading.Tasks;
using Xunit;

namespace GameInterface.Tests.Policies;

public class CallOriginalPolicyTests
{
    [Fact]
    public async Task AllThreadsScope_IsVisibleOnWorkerThreadUntilDisposed()
    {
        Assert.False(CallOriginalPolicy.AreOriginalsAllowedOnAllThreads);

        using (CallOriginalPolicy.AllowOriginalsOnAllThreads())
        {
            Assert.True(CallOriginalPolicy.AreOriginalsAllowedOnAllThreads);
            Assert.True(await Task.Run(CallOriginalPolicy.IsOriginalAllowed));
        }

        Assert.False(CallOriginalPolicy.AreOriginalsAllowedOnAllThreads);
    }

    [Fact]
    public void NestedAllThreadsScope_DoesNotRevokeOuterScope()
    {
        using (CallOriginalPolicy.AllowOriginalsOnAllThreads())
        {
            using (CallOriginalPolicy.AllowOriginalsOnAllThreads())
            {
                Assert.True(CallOriginalPolicy.AreOriginalsAllowedOnAllThreads);
            }

            Assert.True(CallOriginalPolicy.AreOriginalsAllowedOnAllThreads);
        }

        Assert.False(CallOriginalPolicy.AreOriginalsAllowedOnAllThreads);
    }

    [Fact]
    public void AllThreadsScope_DoubleDispose_DoesNotRevokeAnotherScope()
    {
        using (CallOriginalPolicy.AllowOriginalsOnAllThreads())
        {
            var scope = CallOriginalPolicy.AllowOriginalsOnAllThreads();
            scope.Dispose();
            scope.Dispose();

            Assert.True(CallOriginalPolicy.AreOriginalsAllowedOnAllThreads);
        }

        Assert.False(CallOriginalPolicy.AreOriginalsAllowedOnAllThreads);
    }
}
