using Common.Messaging;
using Xunit;

namespace Common.Tests.Messaging;

public class MessageBrokerRespondTests
{
    private record TestResponse : IResponse;

    private sealed class Target
    {
        public bool Handled;
        public int HandledOnThread = -1;

        public void Handle(MessagePayload<TestResponse> payload)
        {
            Handled = true;
            HandledOnThread = Environment.CurrentManagedThreadId;
        }
    }

    [Fact]
    public void RespondSync_InvokesTarget_BeforeReturning_OnCallingThread()
    {
        var broker = new MessageBroker();
        var target = new Target();
        broker.Subscribe<TestResponse>(target.Handle);

        broker.RespondSync(target, new TestResponse());

        // Synchronous: the handler has already run by the time RespondSync returns,
        // on the calling thread — nothing can interleave between caller and response.
        Assert.True(target.Handled);
        Assert.Equal(Environment.CurrentManagedThreadId, target.HandledOnThread);
    }

    [Fact]
    public void RespondSync_OnlyInvokes_TheMatchingTarget()
    {
        var broker = new MessageBroker();
        var intended = new Target();
        var other = new Target();
        broker.Subscribe<TestResponse>(intended.Handle);
        broker.Subscribe<TestResponse>(other.Handle);

        broker.RespondSync(intended, new TestResponse());

        Assert.True(intended.Handled);
        Assert.False(other.Handled);
    }
}
