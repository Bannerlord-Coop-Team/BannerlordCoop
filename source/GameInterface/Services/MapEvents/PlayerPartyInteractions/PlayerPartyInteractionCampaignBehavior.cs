using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

public class PlayerPartyInteractionCampaignBehavior : CampaignBehaviorBase
{
    private const string RootToken = "start";
    private const string InitialToken = "coop_player_party_interaction_initial";
    private const string ServiceToken = "coop_player_party_interaction_services";
    private const string ResponderToken = "coop_player_party_interaction_responder";
    private const string InitiatorWaitToken = "coop_player_party_interaction_initiator_wait";
    private const string CloseToken = "close_window";
    private const int PlayerPartyDialogPriority = 10000;

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, AddDialogs);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void AddDialogs(CampaignGameStarter starter)
    {
        starter.AddDialogLine(
            "coop_player_party_interaction_initial_line",
            RootToken,
            InitialToken,
            "{=coop_player_party_interaction_initial}{COOP_PLAYER_PARTY_INTERACTION_TEXT}",
            () => IsPhase(PlayerPartyInteractionPhase.InitialOptions),
            null,
            PlayerPartyDialogPriority,
            null);

        starter.AddDialogLine(
            "coop_player_party_interaction_services_line",
            ServiceToken,
            ServiceToken,
            "{=coop_player_party_interaction_services}{COOP_PLAYER_PARTY_INTERACTION_TEXT}",
            () => IsPhase(PlayerPartyInteractionPhase.OfferServices),
            null,
            PlayerPartyDialogPriority,
            null);

        starter.AddDialogLine(
            "coop_player_party_interaction_responder_line",
            RootToken,
            ResponderToken,
            "{=coop_player_party_interaction_responder}{COOP_PLAYER_PARTY_INTERACTION_TEXT}",
            () => IsPhase(PlayerPartyInteractionPhase.WaitingForProposal, PlayerPartyInteractionPhase.ProposalPending),
            null,
            PlayerPartyDialogPriority,
            null);

        starter.AddDialogLine(
            "coop_player_party_interaction_initiator_wait_line",
            InitiatorWaitToken,
            InitiatorWaitToken,
            "{=coop_player_party_interaction_wait}{COOP_PLAYER_PARTY_INTERACTION_TEXT}",
            () => IsPhase(PlayerPartyInteractionPhase.WaitingForResponse, PlayerPartyInteractionPhase.TradeActive),
            null,
            PlayerPartyDialogPriority,
            null);

        starter.AddPlayerLine(
            "coop_player_party_interaction_trade",
            InitialToken,
            InitiatorWaitToken,
            "I have a proposal that may benefit us both.",
            () => PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.TradeProposal),
            () => PlayerPartyInteractionDialogState.Submit(PlayerPartyInteractionOption.TradeProposal),
            PlayerPartyDialogPriority,
            null,
            null);

        starter.AddPlayerLine(
            "coop_player_party_interaction_services",
            InitialToken,
            ServiceToken,
            "I wish to offer my services.",
            () => PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.OfferServices),
            PlayerPartyInteractionDialogState.ShowServiceOptions,
            PlayerPartyDialogPriority,
            null,
            null);

        starter.AddPlayerLine(
            "coop_player_party_interaction_join_clan",
            ServiceToken,
            InitiatorWaitToken,
            "I wish to offer my services in your clan.",
            () => PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.JoinClan),
            () => PlayerPartyInteractionDialogState.Submit(PlayerPartyInteractionOption.JoinClan),
            PlayerPartyDialogPriority,
            null,
            null);

        starter.AddPlayerLine(
            "coop_player_party_interaction_vassal",
            ServiceToken,
            InitiatorWaitToken,
            "I wish to swear my allegiance to your majesty.",
            () => PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Vassal),
            () => PlayerPartyInteractionDialogState.Submit(PlayerPartyInteractionOption.Vassal),
            PlayerPartyDialogPriority,
            null,
            null);

        starter.AddPlayerLine(
            "coop_player_party_interaction_accept",
            ResponderToken,
            InitiatorWaitToken,
            "I accept.",
            () => PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.AcceptProposal),
            () => PlayerPartyInteractionDialogState.Submit(PlayerPartyInteractionOption.AcceptProposal),
            PlayerPartyDialogPriority,
            null,
            null);

        starter.AddPlayerLine(
            "coop_player_party_interaction_decline",
            ResponderToken,
            CloseToken,
            "I decline.",
            () => PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.DeclineProposal),
            () => PlayerPartyInteractionDialogState.Submit(PlayerPartyInteractionOption.DeclineProposal),
            PlayerPartyDialogPriority,
            null,
            null);

        starter.AddPlayerLine(
            "coop_player_party_interaction_leave_initial",
            InitialToken,
            CloseToken,
            "I must leave now.",
            () => PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Leave),
            () => PlayerPartyInteractionDialogState.Submit(PlayerPartyInteractionOption.Leave),
            PlayerPartyDialogPriority,
            null,
            null);

        starter.AddPlayerLine(
            "coop_player_party_interaction_leave_services",
            ServiceToken,
            CloseToken,
            "Nevermind.",
            () => PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Leave),
            () => PlayerPartyInteractionDialogState.Submit(PlayerPartyInteractionOption.Leave),
            PlayerPartyDialogPriority,
            null,
            null);

        starter.AddPlayerLine(
            "coop_player_party_interaction_leave_responder",
            ResponderToken,
            CloseToken,
            "I must leave now.",
            () => PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Leave),
            () => PlayerPartyInteractionDialogState.Submit(PlayerPartyInteractionOption.Leave),
            PlayerPartyDialogPriority,
            null,
            null);
    }

    private static bool IsPhase(params PlayerPartyInteractionPhase[] phases)
    {
        MBTextManager.SetTextVariable("COOP_PLAYER_PARTY_INTERACTION_TEXT", PlayerPartyInteractionDialogState.GetDialogText());

        foreach (var phase in phases)
        {
            if (PlayerPartyInteractionDialogState.Phase == phase)
                return true;
        }

        return false;
    }
}
