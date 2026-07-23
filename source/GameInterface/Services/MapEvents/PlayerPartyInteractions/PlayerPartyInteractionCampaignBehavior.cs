using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

public class PlayerPartyInteractionCampaignBehavior : CampaignBehaviorBase
{
    private const string RootToken = "start";
    private const string InitialToken = "coop_player_party_interaction_initial";
    private const string ServiceToken = "coop_player_party_interaction_services";
    private const string ResponderToken = "coop_player_party_interaction_responder";
    private const string HostileConfirmToken = "coop_player_party_interaction_hostile_confirm";
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

        starter.AddDialogLine(
            "coop_player_party_interaction_hostile_confirm_line",
            HostileConfirmToken,
            HostileConfirmToken,
            "{=coop_player_party_hostile_prompt}Eh? What do you want?",
            () => IsPhase(PlayerPartyInteractionPhase.HostileDemandConfirm),
            null,
            PlayerPartyDialogPriority,
            null);

        starter.AddDialogLine(
            "coop_player_party_interaction_hostile_responder_line",
            RootToken,
            ResponderToken,
            "{=coop_player_party_hostile_offer_bubble}I offer you one chance to surrender or die",
            () => IsPhase(PlayerPartyInteractionPhase.HostileDemandPending),
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
            IsTradeProposalEnabled,
            null);

        starter.AddPlayerLine(
            "coop_player_party_interaction_services",
            InitialToken,
            ServiceToken,
            "I wish to offer my services.",
            () => PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.OfferServices),
            PlayerPartyInteractionDialogState.ShowServiceOptions,
            PlayerPartyDialogPriority,
            IsOfferServicesEnabled,
            null);

        starter.AddPlayerLine(
            "coop_player_party_interaction_hostile_demand",
            InitialToken,
            HostileConfirmToken,
            "{=VrnlUvV8}I'm here to deliver you my demands!",
            () => PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.HostileDemand),
            () => PlayerPartyInteractionDialogState.Submit(PlayerPartyInteractionOption.HostileDemand),
            PlayerPartyDialogPriority,
            IsHostileDemandEnabled,
            null);

        starter.AddPlayerLine(
            "coop_player_party_interaction_join_clan",
            ServiceToken,
            InitiatorWaitToken,
            "(COMING SOON) I wish to offer my services in your clan.",
            () => PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.JoinClan),
            () => PlayerPartyInteractionDialogState.Submit(PlayerPartyInteractionOption.JoinClan),
            PlayerPartyDialogPriority,
            IsJoinClanEnabled,
            null);

        starter.AddPlayerLine(
            "coop_player_party_interaction_vassal",
            ServiceToken,
            InitiatorWaitToken,
            "I wish to swear my allegiance to your majesty.",
            () => PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Vassal),
            () => PlayerPartyInteractionDialogState.Submit(PlayerPartyInteractionOption.Vassal),
            PlayerPartyDialogPriority,
            IsVassalEnabled,
            null);

        starter.AddPlayerLine(
            "coop_player_party_interaction_hostile_confirm",
            HostileConfirmToken,
            InitiatorWaitToken,
            "{=coop_player_party_hostile_offer}I offer you one chance to surrender or die.",
            () => PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.ConfirmHostileDemand),
            () => PlayerPartyInteractionDialogState.Submit(PlayerPartyInteractionOption.ConfirmHostileDemand),
            PlayerPartyDialogPriority,
            null,
            null);

        starter.AddPlayerLine(
            "coop_player_party_interaction_hostile_cancel",
            HostileConfirmToken,
            CloseToken,
            "{=coop_player_party_hostile_cancel}Forgive me. It's nothing.",
            () => PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.CancelHostileDemand),
            () => PlayerPartyInteractionDialogState.Submit(PlayerPartyInteractionOption.CancelHostileDemand),
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
            "coop_player_party_interaction_hostile_refuse",
            ResponderToken,
            CloseToken,
            "{=jBN2LlgF}We'll fight to our last drop of blood!",
            () => PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.RefuseHostileDemand),
            () => PlayerPartyInteractionDialogState.Submit(PlayerPartyInteractionOption.RefuseHostileDemand),
            PlayerPartyDialogPriority,
            null,
            null);

        starter.AddPlayerLine(
            "coop_player_party_interaction_hostile_yield",
            ResponderToken,
            CloseToken,
            "{=coop_player_party_hostile_yield}I yield! Call your men off.",
            () => PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.YieldHostileDemand),
            () => PlayerPartyInteractionDialogState.Submit(PlayerPartyInteractionOption.YieldHostileDemand),
            PlayerPartyDialogPriority,
            IsYieldHostileDemandEnabled,
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

    private static bool IsTradeProposalEnabled(out TextObject explanation)
        => PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.TradeProposal, out explanation);

    private static bool IsOfferServicesEnabled(out TextObject explanation)
        => PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.OfferServices, out explanation);

    private static bool IsJoinClanEnabled(out TextObject explanation)
        => PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.JoinClan, out explanation);

    private static bool IsVassalEnabled(out TextObject explanation)
        => PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.Vassal, out explanation);

    private static bool IsHostileDemandEnabled(out TextObject explanation)
        => PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.HostileDemand, out explanation);

    private static bool IsYieldHostileDemandEnabled(out TextObject explanation)
        => PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.YieldHostileDemand, out explanation);

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
