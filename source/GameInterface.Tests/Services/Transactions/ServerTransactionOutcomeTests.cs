using GameInterface.Services.Transactions;
using LiteNetLib;
using System;
using Xunit;

namespace GameInterface.Tests.Services.Transactions;

public class ServerTransactionOutcomeTests
{
    [Fact]
    public void Execute_WithoutExternalAuthorizationBridge_RunsAuthoritativeAction()
    {
        bool actionRan = false;
        bool completed = false;
        Action<NetPeer, int, bool, string> listener = (_, kind, success, reason) =>
        {
            Assert.Equal(ServerTransactionOutcome.Trade, kind);
            Assert.True(success);
            Assert.Empty(reason);
            completed = true;
        };

        ServerTransactionOutcome.Completed += listener;
        try
        {
            ServerTransactionOutcome.Execute(null, ServerTransactionOutcome.Trade, () =>
            {
                actionRan = true;
                ServerTransactionOutcome.Accept(null, ServerTransactionOutcome.Trade);
            });
        }
        finally
        {
            ServerTransactionOutcome.Completed -= listener;
        }

        Assert.True(actionRan);
        Assert.True(completed);
    }

    [Fact]
    public void Execute_WhenHandlerDoesNotReportResult_RejectsTransaction()
    {
        bool? success = null;
        string rejection = null;
        Action<NetPeer, int, bool, string> listener = (_, kind, accepted, reason) =>
        {
            if (kind != ServerTransactionOutcome.Party) return;
            success = accepted;
            rejection = reason;
        };

        ServerTransactionOutcome.Completed += listener;
        try
        {
            ServerTransactionOutcome.Execute(
                null,
                ServerTransactionOutcome.Party,
                () => { });
        }
        finally
        {
            ServerTransactionOutcome.Completed -= listener;
        }

        Assert.False(success);
        Assert.Equal(
            "The authoritative handler did not report a result.",
            rejection);
    }
}
