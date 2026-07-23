using Common.Util;

namespace Common.Tests.Utils
{
    public class AllowedThreadTests
    {
        [Fact]
        public void SingleScope_RevokesOnDispose()
        {
            Assert.False(AllowedThread.IsThisThreadAllowed());

            using (new AllowedThread())
            {
                Assert.True(AllowedThread.IsThisThreadAllowed());
            }

            Assert.False(AllowedThread.IsThisThreadAllowed());
        }

        [Fact]
        public void NestedScope_DoesNotRevokeOuterScope()
        {
            using (new AllowedThread())
            {
                using (new AllowedThread())
                {
                    Assert.True(AllowedThread.IsThisThreadAllowed());
                }

                // The inner dispose must not revoke the outer allowance; otherwise a nested
                // replicated action would re-enable patch interception for the rest of the
                // outer action.
                Assert.True(AllowedThread.IsThisThreadAllowed());
            }

            Assert.False(AllowedThread.IsThisThreadAllowed());
        }

        [Fact]
        public void UnbalancedRevoke_IsHarmless()
        {
            AllowedThread.RevokeThisThread();

            Assert.False(AllowedThread.IsThisThreadAllowed());

            using (new AllowedThread())
            {
                Assert.True(AllowedThread.IsThisThreadAllowed());
            }

            Assert.False(AllowedThread.IsThisThreadAllowed());
        }
    }
}
