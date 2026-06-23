using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class PlayerPartyInteractionDialogStateTests
{
    [Fact]
    public void WaitingForProposal_UsesStableText()
    {
        try
        {
            PlayerPartyInteractionDialogState.Apply(new NetworkPlayerPartyInteractionState(
                "session-1",
                "party-1",
                "party-2",
                "RandomPlayer",
                PlayerPartyInteractionPhase.WaitingForProposal,
                PlayerPartyInteractionProposal.None,
                new[] { PlayerPartyInteractionOption.Leave },
                isInitiator: false));

            Assert.Equal("Awaiting proposal from RandomPlayer...", PlayerPartyInteractionDialogState.GetDialogText());
        }
        finally
        {
            PlayerPartyInteractionDialogState.Clear("session-1");
        }
    }

    [Fact]
    public void WaitingForResponse_UsesStableText()
    {
        try
        {
            PlayerPartyInteractionDialogState.Apply(new NetworkPlayerPartyInteractionState(
                "session-1",
                "party-1",
                "party-2",
                "RandomPlayer",
                PlayerPartyInteractionPhase.WaitingForResponse,
                PlayerPartyInteractionProposal.Trade,
                new PlayerPartyInteractionOption[0],
                isInitiator: true));

            Assert.Equal("Awaiting response from RandomPlayer...", PlayerPartyInteractionDialogState.GetDialogText());
        }
        finally
        {
            PlayerPartyInteractionDialogState.Clear("session-1");
        }
    }
}
