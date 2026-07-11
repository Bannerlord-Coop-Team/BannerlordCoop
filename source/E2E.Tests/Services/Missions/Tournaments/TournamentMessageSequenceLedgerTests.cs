using Missions.Tournaments;

namespace E2E.Tests.Services.Missions.Tournaments;

public class TournamentMessageSequenceLedgerTests
{
    [Fact]
    public void DuplicateAndStaleMessages_AreRejectedPerOrigin()
    {
        var ledger = new TournamentMessageSequenceLedger();

        Assert.True(ledger.TryAccept("host-a", 2));
        Assert.False(ledger.TryAccept("host-a", 2));
        Assert.False(ledger.TryAccept("host-a", 1));
        Assert.True(ledger.TryAccept("host-b", 1));
        Assert.True(ledger.TryAccept("host-a", 3));
    }

    [Fact]
    public void Clear_StartsFreshMatchStreamsWithoutAcceptingInvalidSequences()
    {
        var ledger = new TournamentMessageSequenceLedger();
        Assert.True(ledger.TryAccept("host", 7));

        ledger.Clear();

        Assert.True(ledger.TryAccept("host", 1));
        Assert.False(ledger.TryAccept("host", 0));
        Assert.False(ledger.TryAccept(null, 2));
    }
}
