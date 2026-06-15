using Common;
using GameInterface.Utils;
using Xunit;

namespace Coop.Tests.GameInterface.Utils;

public class MainThreadDispatchTests
{
    [Fact]
    public void RunWhenCampaignReady_NoCampaignLoaded_DefersAndSkipsActionWithoutThrowing()
    {
        // Coop.Tests runs a dedicated game-loop pump thread (TestGameLoopPump), and no
        // campaign is loaded in the unit-test environment, so the guarded action must be
        // deferred to that thread and then skipped, without any exception escaping.
        var ran = false;

        MainThreadDispatch.RunWhenCampaignReady("unit test", () => ran = true);

        // FIFO barrier: a blocking action queued after the helper's returns only once the
        // pump has drained everything before it, so the helper's action has been processed.
        GameLoopRunner.RunOnMainThread(() => { }, blocking: true);

        Assert.False(ran);
    }
}
