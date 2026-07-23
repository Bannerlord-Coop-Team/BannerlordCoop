using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages.Conversation;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.Localization;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

public static class PlayerPartyInteractionDialogState
{
    private const BindingFlags InstanceBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string MapConversationVmTypeName = "TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapConversation.MapConversationVM";

    private static readonly ILogger Logger = LogManager.GetLogger(typeof(PlayerPartyInteractionDialogState));

    private static NetworkPlayerPartyInteractionState currentState;
    private static bool hasState;

    public static string SessionId => hasState ? currentState.SessionId : null;
    public static string PartyId => hasState ? currentState.PartyId : null;
    public static string OtherPartyId => hasState ? currentState.OtherPartyId : null;
    public static string OtherPlayerName => hasState ? currentState.OtherPlayerName : "the other player";
    public static PlayerPartyInteractionPhase Phase => hasState ? currentState.Phase : PlayerPartyInteractionPhase.None;
    public static PlayerPartyInteractionProposal Proposal => hasState ? currentState.Proposal : PlayerPartyInteractionProposal.None;
    public static bool InitiatorAcceptedTrade => hasState && currentState.InitiatorAcceptedTrade;
    public static bool ResponderAcceptedTrade => hasState && currentState.ResponderAcceptedTrade;
    public static bool IsHostile => hasState && currentState.IsHostile;
    public static bool HasActiveState => hasState;

    internal static void Apply(NetworkPlayerPartyInteractionState state)
    {
        currentState = state;
        hasState = true;
        RefreshConversation();
    }

    public static void Clear(string sessionId = null)
    {
        if (sessionId != null && hasState && currentState.SessionId != sessionId) return;

        hasState = false;
        currentState = default;
    }

    public static bool HasOption(PlayerPartyInteractionOption option)
        => hasState && currentState.Options != null && currentState.Options.Contains(option);

    public static bool IsOptionEnabled(PlayerPartyInteractionOption option)
    {
        if (!HasOption(option)) return false;

        var enabledOptions = currentState.EnabledOptions ?? currentState.Options;
        return enabledOptions != null && enabledOptions.Contains(option);
    }

    public static bool IsOptionEnabled(PlayerPartyInteractionOption option, out TextObject explanation)
    {
        if (IsOptionEnabled(option))
        {
            explanation = null;
            return true;
        }

        if (option == PlayerPartyInteractionOption.OfferServices && IsHostile)
        {
            explanation = new TextObject("{=coop_player_party_interaction_hostile_disabled}Not available while hostile");
            return false;
        }

        if (option == PlayerPartyInteractionOption.Vassal && TryGetVassalUnavailableExplanation(out explanation))
            return false;

        explanation = new TextObject("{=coop_player_party_interaction_disabled}This option is not available.");
        return false;
    }

    private static bool TryGetVassalUnavailableExplanation(out TextObject explanation)
    {
        switch (currentState.VassalUnavailableReason)
        {
            case PlayerPartyInteractionVassalUnavailableReason.TargetIsNotKingdomLeader:
                explanation = new TextObject("{=coop_player_party_interaction_vassal_target_not_ruler}The other player must rule a kingdom.");
                return true;
            case PlayerPartyInteractionVassalUnavailableReason.InitiatorHasNoClan:
                explanation = new TextObject("{=coop_player_party_interaction_vassal_requires_clan}You must lead a clan to swear allegiance.");
                return true;
            case PlayerPartyInteractionVassalUnavailableReason.InitiatorIsInKingdom:
                explanation = new TextObject("{=coop_player_party_interaction_vassal_already_in_kingdom}You must leave your current kingdom first.");
                return true;
            case PlayerPartyInteractionVassalUnavailableReason.InitiatorClanTierTooLow:
                explanation = new TextObject("{=coop_player_party_interaction_vassal_requires_tier_two}Your clan must be at least tier 2 to swear allegiance.");
                return true;
            default:
                explanation = null;
                return false;
        }
    }

    public static string GetDialogText()
    {
        switch (Phase)
        {
            case PlayerPartyInteractionPhase.WaitingForProposal:
                return $"Awaiting proposal from {OtherPlayerName}...";
            case PlayerPartyInteractionPhase.WaitingForResponse:
                return $"Awaiting response from {OtherPlayerName}...";
            case PlayerPartyInteractionPhase.ProposalPending:
                return GetProposalText();
            case PlayerPartyInteractionPhase.HostileDemandConfirm:
                return "Eh? What do you want?";
            case PlayerPartyInteractionPhase.HostileDemandPending:
                return "I offer you one chance to surrender or die";
            case PlayerPartyInteractionPhase.TradeActive:
                return "Let us review the trade.";
            case PlayerPartyInteractionPhase.OfferServices:
                return "What service do you wish to offer?";
            default:
                return "What would you like to discuss?";
        }
    }

    public static void Submit(PlayerPartyInteractionOption option)
    {
        var enabled = IsOptionEnabled(option);
        Logger.Information(
            "[P2POptionTrace] Local player-party dialog option clicked; sessionId={SessionId} partyId={PartyId} otherPartyId={OtherPartyId} option={Option} enabled={Enabled} phase={Phase} proposal={Proposal} isHostile={IsHostile}",
            SessionId ?? "<none>",
            PartyId ?? "<none>",
            OtherPartyId ?? "<none>",
            option,
            enabled,
            Phase,
            Proposal,
            IsHostile);

        if (!enabled) return;

        MessageBroker.Instance.Publish(null, new PlayerPartyInteractionOptionSelected(SessionId, PartyId, option));
    }

    public static void ShowServiceOptions()
    {
        var enabled = HasActiveState &&
                      Phase == PlayerPartyInteractionPhase.InitialOptions &&
                      IsOptionEnabled(PlayerPartyInteractionOption.OfferServices);
        Logger.Information(
            "[P2POptionTrace] Local player-party dialog option clicked; sessionId={SessionId} partyId={PartyId} otherPartyId={OtherPartyId} option={Option} enabled={Enabled} phase={Phase} proposal={Proposal} isHostile={IsHostile}",
            SessionId ?? "<none>",
            PartyId ?? "<none>",
            OtherPartyId ?? "<none>",
            PlayerPartyInteractionOption.OfferServices,
            enabled,
            Phase,
            Proposal,
            IsHostile);

        if (!HasActiveState) return;
        if (Phase != PlayerPartyInteractionPhase.InitialOptions) return;
        if (!IsOptionEnabled(PlayerPartyInteractionOption.OfferServices)) return;

        currentState = new NetworkPlayerPartyInteractionState(
            currentState.SessionId,
            currentState.PartyId,
            currentState.OtherPartyId,
            currentState.OtherPlayerName,
            PlayerPartyInteractionPhase.OfferServices,
            PlayerPartyInteractionProposal.None,
            GetLocalServiceOptions(),
            currentState.IsInitiator,
            currentState.InitiatorAcceptedTrade,
            currentState.ResponderAcceptedTrade,
            currentState.PartyItems,
            currentState.OtherPartyItems,
            GetLocalServiceEnabledOptions(),
            currentState.IsHostile,
            currentState.VassalUnavailableReason);

        RefreshConversation();
    }

    private static string GetProposalText()
    {
        switch (Proposal)
        {
            case PlayerPartyInteractionProposal.Trade:
                return "I have a proposal that may benefit us both.";
            case PlayerPartyInteractionProposal.JoinClan:
                return "(COMING SOON) I wish to offer my services in your clan.";
            case PlayerPartyInteractionProposal.Vassal:
                return "I wish to swear my allegiance to your majesty.";
            case PlayerPartyInteractionProposal.HostileDemand:
                return "I offer you one chance to surrender or die";
            default:
                return $"{OtherPlayerName} has made a proposal.";
        }
    }

    private static PlayerPartyInteractionOption[] GetLocalServiceOptions()
        => GetLocalServiceOptions(currentState.Options, addLeave: true);

    private static PlayerPartyInteractionOption[] GetLocalServiceEnabledOptions()
        => GetLocalServiceOptions(currentState.EnabledOptions ?? currentState.Options, addLeave: false);

    private static PlayerPartyInteractionOption[] GetLocalServiceOptions(PlayerPartyInteractionOption[] sourceOptions, bool addLeave)
    {
        var options = new List<PlayerPartyInteractionOption>();
        if (sourceOptions != null)
        {
            foreach (var option in sourceOptions)
            {
                if (!IsServiceOption(option)) continue;
                if (options.Contains(option)) continue;

                options.Add(option);
            }
        }

        if (addLeave && !options.Contains(PlayerPartyInteractionOption.Leave))
            options.Add(PlayerPartyInteractionOption.Leave);

        return options.ToArray();
    }

    private static bool IsServiceOption(PlayerPartyInteractionOption option)
        => option == PlayerPartyInteractionOption.JoinClan ||
           option == PlayerPartyInteractionOption.Vassal ||
           option == PlayerPartyInteractionOption.Leave;

    internal static void RefreshConversation()
    {
        var conversationManager = Campaign.Current?.ConversationManager;
        if (conversationManager == null || !conversationManager.IsConversationInProgress) return;

        MBTextManager.SetTextVariable("COOP_PLAYER_PARTY_INTERACTION_TEXT", GetDialogText());
        conversationManager.UpdateCurrentSentenceText();
        conversationManager.ClearCurrentOptions();
        if (ShouldRefreshOptions())
            conversationManager.GetPlayerSentenceOptions();

        RefreshConversationVm(conversationManager);
    }

    private static bool ShouldRefreshOptions()
        => currentState.Options != null &&
           currentState.Options.Length > 0;

    private static void RefreshConversationVm(ConversationManager conversationManager)
    {
        try
        {
            var handler = conversationManager.Handler;
            var mapConversationVm = GetMapConversationVm(handler);
            var dialogController = mapConversationVm?.GetType()
                .GetProperty("DialogController", InstanceBindingFlags)?
                .GetValue(mapConversationVm);
            var refresh = dialogController?.GetType().GetMethod("Refresh", InstanceBindingFlags);

            refresh?.Invoke(dialogController, Array.Empty<object>());
        }
        catch (Exception)
        {
        }
    }

    private static object GetMapConversationVm(object handler)
    {
        if (handler == null) return null;

        var handlerType = handler.GetType();
        var dataSource = handlerType.GetField("_dataSource", InstanceBindingFlags)?.GetValue(handler);
        if (IsMapConversationVm(dataSource)) return dataSource;

        foreach (var field in handlerType.GetFields(InstanceBindingFlags))
        {
            var fieldValue = field.GetValue(handler);
            if (IsMapConversationVm(fieldValue)) return fieldValue;
        }

        foreach (var property in handlerType.GetProperties(InstanceBindingFlags))
        {
            if (property.GetIndexParameters().Length > 0) continue;

            object propertyValue;
            try
            {
                propertyValue = property.GetValue(handler);
            }
            catch (Exception)
            {
                continue;
            }

            if (IsMapConversationVm(propertyValue)) return propertyValue;
        }

        return null;
    }

    private static bool IsMapConversationVm(object value)
    {
        if (value == null) return false;

        var type = value.GetType();
        return type.FullName == MapConversationVmTypeName ||
               type.GetProperty("DialogController", InstanceBindingFlags) != null;
    }
}
