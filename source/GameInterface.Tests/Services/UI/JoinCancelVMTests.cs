using Common.Messaging;
using GameInterface.Services.UI.JoinCancel;
using GameInterface.Services.UI.Messages;
using System;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class JoinCancelVMTests
{
    [Fact]
    public void ActionCancel_PublishesTheCancelRequest()
    {
        using var messageBroker = new MessageBroker();
        var requests = Subscribe(messageBroker, out var subscription);
        var viewModel = new JoinCancelVM("Cancel", messageBroker);

        viewModel.ActionCancel();

        Assert.Single(requests);
        GC.KeepAlive(subscription);
    }

    [Fact]
    public void ActionCancel_StaysUsableAfterARequestThatWasDropped()
    {
        using var messageBroker = new MessageBroker();
        var handled = 0;
        // The broker swallows a handler's exception, so a press whose teardown blew up is
        // indistinguishable from one that worked. Either way the player must be able to press again.
        Action<MessagePayload<CancelJoinAttempt>> subscription = _ =>
        {
            handled++;
            if (handled == 1) throw new InvalidOperationException("the session was already gone");
        };
        messageBroker.Subscribe(subscription);
        var viewModel = new JoinCancelVM("Cancel", messageBroker);

        viewModel.ActionCancel();
        viewModel.ActionCancel();

        Assert.Equal(2, handled);
        GC.KeepAlive(subscription);
    }

    [Fact]
    public void CancelButtonText_TakesTheLabelItWasGiven()
    {
        using var messageBroker = new MessageBroker();
        var viewModel = new JoinCancelVM("Stop waiting", messageBroker);

        Assert.Equal("Stop waiting", viewModel.CancelButtonText);

        viewModel.CancelButtonText = "Cancel";

        Assert.Equal("Cancel", viewModel.CancelButtonText);
    }

    // The broker holds subscriptions weakly, so the handler has to be kept alive by the test.
    private static List<CancelJoinAttempt> Subscribe(
        IMessageBroker messageBroker,
        out Action<MessagePayload<CancelJoinAttempt>> subscription)
    {
        var requests = new List<CancelJoinAttempt>();
        subscription = payload => requests.Add(payload.What);
        messageBroker.Subscribe(subscription);
        return requests;
    }
}
