using Autofac;
using GameInterface.Policies;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GameInterface.Tests.Policies;

/// <summary>
/// Covers when <see cref="CallOriginalPolicy"/> lets patched vanilla methods run.
/// </summary>
[Collection(ContainerProviderCollection.Name)]
public class CallOriginalPolicyTests
{
    [Fact]
    public void NoActiveContainer_AllowsOriginals()
    {
        bool hadPreviousContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        try
        {
            ContainerProvider.Clear();

            Assert.True(CallOriginalPolicy.IsOriginalAllowed());
            Assert.True(CallOriginalPolicy.IsOriginalAllowedForOwnershipGate());
        }
        finally
        {
            RestoreContainer(hadPreviousContainer, previousContainer);
        }
    }

    [Fact]
    public void ContainerWithoutSyncPolicy_AllowsOriginals()
    {
        using var container = new ContainerBuilder().Build();
        bool hadPreviousContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        try
        {
            using (ContainerProvider.UseContainerThreadSafe(container))
            {
                Assert.True(CallOriginalPolicy.IsOriginalAllowed());
                Assert.True(CallOriginalPolicy.IsOriginalAllowedForOwnershipGate());
            }
        }
        finally
        {
            RestoreContainer(hadPreviousContainer, previousContainer);
        }
    }

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

    [Fact]
    public void CurrentOperationScope_IsLimitedToInvokingThread()
    {
        Assert.False(CallOriginalPolicy.AreOriginalsAllowedForCurrentOperation);

        using (CallOriginalPolicy.AllowOriginalsForCurrentOperation())
        {
            Assert.True(CallOriginalPolicy.AreOriginalsAllowedForCurrentOperation);
            Assert.True(CallOriginalPolicy.IsOriginalAllowed());

            bool originalsAllowedOnWorkerThread = true;
            var workerThread = new Thread(() =>
                originalsAllowedOnWorkerThread = CallOriginalPolicy.AreOriginalsAllowedForCurrentOperation);
            workerThread.Start();
            workerThread.Join();

            Assert.False(originalsAllowedOnWorkerThread);
        }

        Assert.False(CallOriginalPolicy.AreOriginalsAllowedForCurrentOperation);
    }

    [Fact]
    public void NestedCurrentOperationScope_DoesNotRevokeOuterScope()
    {
        using (CallOriginalPolicy.AllowOriginalsForCurrentOperation())
        {
            using (CallOriginalPolicy.AllowOriginalsForCurrentOperation())
            {
                Assert.True(CallOriginalPolicy.AreOriginalsAllowedForCurrentOperation);
            }

            Assert.True(CallOriginalPolicy.AreOriginalsAllowedForCurrentOperation);
        }

        Assert.False(CallOriginalPolicy.AreOriginalsAllowedForCurrentOperation);
    }

    [Fact]
    public void CurrentOperationScope_DoubleDispose_DoesNotRevokeAnotherScope()
    {
        using (CallOriginalPolicy.AllowOriginalsForCurrentOperation())
        {
            var scope = CallOriginalPolicy.AllowOriginalsForCurrentOperation();
            scope.Dispose();
            scope.Dispose();

            Assert.True(CallOriginalPolicy.AreOriginalsAllowedForCurrentOperation);
        }

        Assert.False(CallOriginalPolicy.AreOriginalsAllowedForCurrentOperation);
    }

    /// <summary>
    /// Puts back the scope that was active before the test so later tests see the same <see cref="ContainerProvider"/> state.
    /// </summary>
    private static void RestoreContainer(bool hadPreviousContainer, ILifetimeScope previousContainer)
    {
        if (!hadPreviousContainer)
        {
            ContainerProvider.Clear();
            return;
        }

        ContainerProvider.SetContainer(previousContainer);
    }
}
