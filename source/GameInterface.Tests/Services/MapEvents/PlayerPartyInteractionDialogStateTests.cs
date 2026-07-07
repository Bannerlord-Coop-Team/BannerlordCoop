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

    [Fact]
    public void HostileInitialOptions_DisabledOfferServicesUsesHostileReason()
    {
        try
        {
            PlayerPartyInteractionDialogState.Apply(new NetworkPlayerPartyInteractionState(
                "session-1",
                "party-1",
                "party-2",
                "RandomPlayer",
                PlayerPartyInteractionPhase.InitialOptions,
                PlayerPartyInteractionProposal.None,
                new[]
                {
                    PlayerPartyInteractionOption.TradeProposal,
                    PlayerPartyInteractionOption.OfferServices
                },
                isInitiator: true,
                enabledOptions: new[] { PlayerPartyInteractionOption.TradeProposal },
                isHostile: true));

            Assert.False(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.OfferServices, out var explanation));
            Assert.Equal("Not available while hostile", explanation.ToString());
            Assert.True(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.TradeProposal));
        }
        finally
        {
            PlayerPartyInteractionDialogState.Clear("session-1");
        }
    }

    [Fact]
    public void HostileDemandConfirm_UsesDemandPromptText()
    {
        try
        {
            PlayerPartyInteractionDialogState.Apply(new NetworkPlayerPartyInteractionState(
                "session-1",
                "party-1",
                "party-2",
                "RandomPlayer",
                PlayerPartyInteractionPhase.HostileDemandConfirm,
                PlayerPartyInteractionProposal.HostileDemand,
                new[]
                {
                    PlayerPartyInteractionOption.ConfirmHostileDemand,
                    PlayerPartyInteractionOption.CancelHostileDemand
                },
                isInitiator: true));

            Assert.Equal("Eh? What do you want?", PlayerPartyInteractionDialogState.GetDialogText());
        }
        finally
        {
            PlayerPartyInteractionDialogState.Clear("session-1");
        }
    }

    [Fact]
    public void HostileDemandPending_UsesOfferTextAndEnablesYieldOption()
    {
        try
        {
            PlayerPartyInteractionDialogState.Apply(new NetworkPlayerPartyInteractionState(
                "session-1",
                "party-1",
                "party-2",
                "RandomPlayer",
                PlayerPartyInteractionPhase.HostileDemandPending,
                PlayerPartyInteractionProposal.HostileDemand,
                new[]
                {
                    PlayerPartyInteractionOption.RefuseHostileDemand,
                    PlayerPartyInteractionOption.YieldHostileDemand
                },
                isInitiator: false));

            Assert.Equal("I offer you one chance to surrender or die", PlayerPartyInteractionDialogState.GetDialogText());
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.RefuseHostileDemand));
            Assert.True(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.RefuseHostileDemand));
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.YieldHostileDemand));
            Assert.True(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.YieldHostileDemand, out var explanation));
            Assert.Null(explanation);
            Assert.False(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Leave));
        }
        finally
        {
            PlayerPartyInteractionDialogState.Clear("session-1");
        }
    }
}
