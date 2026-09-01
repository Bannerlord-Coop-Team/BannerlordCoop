using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Clans;
using GameInterface.Services.Clans.Data;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

public static class PlayerPartyInteractionDialogState
{
    private const BindingFlags InstanceBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string MapConversationVmTypeName = "TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapConversation.MapConversationVM";

    private static readonly ILogger Logger = LogManager.GetLogger(typeof(PlayerPartyInteractionDialogState));

    private static NetworkPlayerPartyInteractionState currentState;
    private static bool hasState;
    private static string clanJoinConfirmationSessionId;
    private static NetworkPlayerPartyInteractionState? marriageInitialState;

    public static string SessionId => hasState ? currentState.SessionId : null;
    public static string PartyId => hasState ? currentState.PartyId : null;
    public static string OtherPartyId => hasState ? currentState.OtherPartyId : null;
    public static string OtherPlayerName => hasState ? currentState.OtherPlayerName : "the other player";
    public static PlayerPartyInteractionPhase Phase => hasState ? currentState.Phase : PlayerPartyInteractionPhase.None;
    public static PlayerPartyInteractionProposal Proposal => hasState ? currentState.Proposal : PlayerPartyInteractionProposal.None;
    public static bool InitiatorAcceptedTrade => hasState && currentState.InitiatorAcceptedTrade;
    public static bool ResponderAcceptedTrade => hasState && currentState.ResponderAcceptedTrade;
    public static bool IsHostile => hasState && currentState.IsHostile;
    public static int MercenaryAwardMultiplier => hasState ? currentState.MercenaryAwardMultiplier : 0;
    public static bool HasActiveState => hasState;
    internal static bool IsInitiator => hasState && currentState.IsInitiator;
    public static bool IsMarriageProposal => Proposal == PlayerPartyInteractionProposal.PatrilinealMarriage ||
        Proposal == PlayerPartyInteractionProposal.MatrilinealMarriage;

    internal static void Apply(NetworkPlayerPartyInteractionState state)
    {
        if (clanJoinConfirmationSessionId != null &&
            (state.SessionId != SessionId || state.Phase != Phase || state.OtherPartyId != OtherPartyId))
            ClearClanJoinConfirmation();

        currentState = state;
        marriageInitialState = null;
        hasState = true;
        RefreshConversation();
    }

    public static void Clear(string sessionId = null)
    {
        if (sessionId != null && hasState && currentState.SessionId != sessionId) return;

        ClearClanJoinConfirmation();
        hasState = false;
        currentState = default;
        marriageInitialState = null;
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

        if (option == PlayerPartyInteractionOption.ProposeMarriage)
        {
            explanation = GameTexts.FindText(IsHostile ? "str_coop_marriage_hostile" : "str_coop_marriage_ineligible");
            return false;
        }
        if (option == PlayerPartyInteractionOption.PatrilinealMarriage || option == PlayerPartyInteractionOption.MatrilinealMarriage)
        {
            explanation = GameTexts.FindText("str_coop_marriage_clan_unavailable");
            return false;
        }

        if (option == PlayerPartyInteractionOption.OfferServices && IsHostile)
        {
            explanation = new TextObject("{=coop_player_party_interaction_hostile_disabled}Not available while hostile");
            return false;
        }

        if (option == PlayerPartyInteractionOption.Vassal && TryGetVassalUnavailableExplanation(out explanation))
            return false;

        if (option == PlayerPartyInteractionOption.JoinClan && TryGetClanJoinUnavailableExplanation(out explanation))
            return false;

        if (option == PlayerPartyInteractionOption.Mercenary && TryGetMercenaryUnavailableExplanation(out explanation))
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

    private static bool TryGetClanJoinUnavailableExplanation(out TextObject explanation)
    {
        var textId = currentState.ClanJoinUnavailableReason switch
        {
            ClanJoinUnavailableReason.OtherPlayersInClan => "str_coop_clan_join_other_players",
            ClanJoinUnavailableReason.TargetIsNotClanLeader => "str_coop_clan_join_target_not_leader",
            ClanJoinUnavailableReason.RulesKingdom => "str_coop_clan_join_rules_kingdom",
            ClanJoinUnavailableReason.Mercenary => "str_coop_clan_join_mercenary",
            ClanJoinUnavailableReason.Vassal => "str_coop_clan_join_vassal",
            ClanJoinUnavailableReason.OwnsFiefs => "str_coop_clan_join_owns_fiefs",
            ClanJoinUnavailableReason.IncompatibleWars => "str_coop_clan_join_incompatible_wars",
            ClanJoinUnavailableReason.TooManyCompanions => "str_coop_clan_join_too_many_companions",
            ClanJoinUnavailableReason.TooManyWorkshops => "str_coop_clan_join_too_many_workshops",
            ClanJoinUnavailableReason.TooManyParties => "str_coop_clan_join_too_many_parties",
            _ => null
        };
        explanation = textId == null ? null : GameTexts.FindText(textId);
        return explanation != null;
    }

    private static bool TryGetMercenaryUnavailableExplanation(out TextObject explanation)
    {
        switch(currentState.MercenaryUnavailableReason)
        {
            case PlayerPartyInteractionMercenaryUnavailableReason.InitiatorHasNoClan:
                explanation = new TextObject("{=coop_player_party_interaction_mercenary_requires_clan}You must have a clan to join as mercenary.");
                return true;
            case PlayerPartyInteractionMercenaryUnavailableReason.InitiatorIsNotClanLeader:
                explanation = new TextObject("{=coop_player_party_interaction_mercenary_is_not_clan_leader}You must lead a clan to join as mercenary.");
                return true;
            case PlayerPartyInteractionMercenaryUnavailableReason.InitiatorClanTierTooLow:
                explanation = new TextObject("{=coop_player_party_interaction_mercenary_requires_tier_one}Your clan must be at least tier 1 to join as mercenary.");
                return true;
            case PlayerPartyInteractionMercenaryUnavailableReason.AlreadyMercenaryForThisKingdom:
                explanation = new TextObject("{=coop_player_party_interaction_mercenary_already_mercenary}Your clan is already a mercenary for this kingdom");
                return true;
            case PlayerPartyInteractionMercenaryUnavailableReason.InitiatorClanHasSettlement:
                explanation = new TextObject("{=coop_player_party_interaction_mercenary_requires_no_settlement}Clans that own a settlement are not considered as mercenaries.");
                    return true;
            case PlayerPartyInteractionMercenaryUnavailableReason.NotEnoughRelation:
                explanation = new TextObject("{=coop_player_party_interaction_mercenary_requires_relation}You need {RELATION} relation with player");
                    explanation.SetTextVariable("RELATION", Campaign.Current.Models.DiplomacyModel.MinimumRelationWithConversationCharacterToJoinKingdom);
                    return true;
            case PlayerPartyInteractionMercenaryUnavailableReason.ClanIsInKingdom:
                explanation = new TextObject("{=coop_player_party_interaction_mercenary_clan_is_in_kingdom}You must leave your current kingdom to apply as a mercenary.");
                    return true;
            case PlayerPartyInteractionMercenaryUnavailableReason.TargetHasNoKingdom:
                explanation = new TextObject("{=coop_player_party_interaction_mercenary_target_has_no_kingdom}The other player must be in a kingdom.");
                return true;
            case PlayerPartyInteractionMercenaryUnavailableReason.IsAtWarWithTarget:
                explanation = new TextObject("{=coop_player_party_interaction_mercenary_is_at_war_with_target}You are at war with this kingdom");
                return true;
            case PlayerPartyInteractionMercenaryUnavailableReason.IncompatibleWars:
                explanation = new TextObject("{=coop_player_party_interaction_mercenary_incompatible_wars}You are at war with a faction the other player's kingdom is not at war with.");
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
            case PlayerPartyInteractionPhase.MarriageOptions:
                return GameTexts.FindText("str_coop_marriage_choose").ToString();
            case PlayerPartyInteractionPhase.MercenaryConfirm:
                var mercenaryConfirmText = new TextObject("{=coop_player_party_mercenary_confirm}Mercenaries receive influence like vassals for fighting, but it is exchanged at the end of each day for denars at the rate of {MERCENARY_AWARD}{GOLD_ICON} per influence point. Do you accept these terms?");
                mercenaryConfirmText.SetTextVariable("MERCENARY_AWARD", MercenaryAwardMultiplier);
                return mercenaryConfirmText.ToString();
            default:
                return "What would you like to discuss?";
        }
    }

    public static void ConfirmClanJoin()
    {
        if (!IsOptionEnabled(PlayerPartyInteractionOption.JoinClan) || clanJoinConfirmationSessionId != null) return;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return;
        if (!objectManager.TryGetObjectWithLogging(OtherPartyId, out PartyBase otherParty)) return;

        var hero = Hero.MainHero;
        var targetClan = otherParty.LeaderHero?.Clan;
        if (hero.Clan == null || targetClan?.Leader == null) return;

        ShowClanJoinConfirmation(hero, targetClan, PlayerPartyInteractionOption.JoinClan);
    }

    public static void ProposeMarriage(bool matrilineal)
        => ConfirmMarriageClanJoin(matrilineal ? PlayerPartyInteractionOption.MatrilinealMarriage :
            PlayerPartyInteractionOption.PatrilinealMarriage, matrilineal);

    public static void AcceptProposal()
    {
        if (IsMarriageProposal)
            ConfirmMarriageClanJoin(PlayerPartyInteractionOption.AcceptProposal, Proposal == PlayerPartyInteractionProposal.MatrilinealMarriage);
        else
            Submit(PlayerPartyInteractionOption.AcceptProposal);
    }

    private static void ConfirmMarriageClanJoin(PlayerPartyInteractionOption option, bool matrilineal)
    {
        if (!IsOptionEnabled(option) || clanJoinConfirmationSessionId != null) return;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return;
        if (!objectManager.TryGetObjectWithLogging(OtherPartyId, out PartyBase otherParty)) return;

        var hero = Hero.MainHero;
        var targetClan = otherParty.LeaderHero?.Clan;
        if (hero.IsFemale == matrilineal || hero.Clan == targetClan)
        {
            Submit(option);
            return;
        }
        if (hero.Clan == null || targetClan?.Leader == null) return;

        ShowClanJoinConfirmation(hero, targetClan, option, option == PlayerPartyInteractionOption.AcceptProposal
            ? ClanJoinConfirmationContext.MarriageAcceptance : ClanJoinConfirmationContext.MarriageProposal);
    }

    private static void ShowClanJoinConfirmation(Hero hero, Clan targetClan, PlayerPartyInteractionOption option,
        ClanJoinConfirmationContext context = ClanJoinConfirmationContext.JoinRequest)
    {
        if (!ContainerProvider.TryResolve<IClanJoinConfirmation>(out var confirmation)) return;
        var sessionId = SessionId;
        clanJoinConfirmationSessionId = sessionId;
        InformationManager.ShowInquiry(confirmation.CreateInquiry(hero, targetClan,
            () =>
            {
                if (clanJoinConfirmationSessionId != sessionId || SessionId != sessionId) return;
                clanJoinConfirmationSessionId = null;
                Submit(option);
            },
            () =>
            {
                if (clanJoinConfirmationSessionId == sessionId)
                    clanJoinConfirmationSessionId = null;
            }, context), false);
    }

    private static void ClearClanJoinConfirmation()
    {
        if (clanJoinConfirmationSessionId == null) return;
        clanJoinConfirmationSessionId = null;
        InformationManager.HideInquiry();
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

        ShowLocalOptions(PlayerPartyInteractionPhase.OfferServices, GetLocalServiceOptions(), GetLocalServiceEnabledOptions());
    }

    public static void ShowMarriageOptions()
    {
        if (Phase != PlayerPartyInteractionPhase.InitialOptions || !IsOptionEnabled(PlayerPartyInteractionOption.ProposeMarriage)) return;

        marriageInitialState = currentState;
        var options = new[] { PlayerPartyInteractionOption.PatrilinealMarriage,
            PlayerPartyInteractionOption.MatrilinealMarriage, PlayerPartyInteractionOption.CancelMarriage };
        var enabledOptions = options.Where(option => option == PlayerPartyInteractionOption.CancelMarriage || IsOptionEnabled(option)).ToArray();
        ShowLocalOptions(PlayerPartyInteractionPhase.MarriageOptions, options, enabledOptions);
    }

    public static void CancelMarriageOptions()
    {
        if (Phase != PlayerPartyInteractionPhase.MarriageOptions || !marriageInitialState.HasValue) return;

        ClearClanJoinConfirmation();
        currentState = marriageInitialState.Value;
        marriageInitialState = null;
        RefreshConversation();
    }

    private static void ShowLocalOptions(PlayerPartyInteractionPhase phase,
        PlayerPartyInteractionOption[] options, PlayerPartyInteractionOption[] enabledOptions)
    {
        currentState = new NetworkPlayerPartyInteractionState(
            currentState.SessionId,
            currentState.PartyId,
            currentState.OtherPartyId,
            currentState.OtherPlayerName,
            phase,
            PlayerPartyInteractionProposal.None,
            options,
            currentState.IsInitiator,
            currentState.MercenaryAwardMultiplier,
            currentState.InitiatorAcceptedTrade,
            currentState.ResponderAcceptedTrade,
            currentState.PartyItems,
            currentState.OtherPartyItems,
            enabledOptions,
            currentState.IsHostile,
            currentState.VassalUnavailableReason,
            currentState.MercenaryUnavailableReason,
            currentState.ClanJoinUnavailableReason);

        RefreshConversation();
    }
    public static void SelectMercenary()
    {
        var enabled = HasActiveState &&
                      Phase == PlayerPartyInteractionPhase.OfferServices &&
                      IsOptionEnabled(PlayerPartyInteractionOption.Mercenary);

        if (!enabled) return;

        Submit(PlayerPartyInteractionOption.Mercenary);

        currentState = new NetworkPlayerPartyInteractionState(
            currentState.SessionId,
            currentState.PartyId,
            currentState.OtherPartyId,
            currentState.OtherPlayerName,
            PlayerPartyInteractionPhase.MercenaryConfirm,
            PlayerPartyInteractionProposal.Mercenary,
            new[] { PlayerPartyInteractionOption.ConfirmMercenary, PlayerPartyInteractionOption.CancelMercenary },
            currentState.IsInitiator,
            currentState.MercenaryAwardMultiplier,
            currentState.InitiatorAcceptedTrade,
            currentState.ResponderAcceptedTrade,
            currentState.PartyItems,
            currentState.OtherPartyItems,
            new[] { PlayerPartyInteractionOption.ConfirmMercenary, PlayerPartyInteractionOption.CancelMercenary },
            currentState.IsHostile,
            currentState.VassalUnavailableReason,
            currentState.MercenaryUnavailableReason,
            currentState.ClanJoinUnavailableReason);

        RefreshConversation();
    }

    public static void ConfirmMercenary()
    {
        var enabled = HasActiveState &&
                      Phase == PlayerPartyInteractionPhase.MercenaryConfirm &&
                      IsOptionEnabled(PlayerPartyInteractionOption.ConfirmMercenary);

        if (!enabled) return;

        Submit(PlayerPartyInteractionOption.ConfirmMercenary);

        currentState = new NetworkPlayerPartyInteractionState(
            currentState.SessionId,
            currentState.PartyId,
            currentState.OtherPartyId,
            currentState.OtherPlayerName,
            PlayerPartyInteractionPhase.WaitingForResponse,
            currentState.Proposal,
            Array.Empty<PlayerPartyInteractionOption>(),
            currentState.IsInitiator,
            currentState.MercenaryAwardMultiplier,
            currentState.InitiatorAcceptedTrade,
            currentState.ResponderAcceptedTrade,
            currentState.PartyItems,
            currentState.OtherPartyItems,
            Array.Empty<PlayerPartyInteractionOption>(),
            currentState.IsHostile,
            currentState.VassalUnavailableReason,
            currentState.MercenaryUnavailableReason,
            currentState.ClanJoinUnavailableReason);

        RefreshConversation();
    }

    private static string GetProposalText()
    {
        switch (Proposal)
        {
            case PlayerPartyInteractionProposal.Trade:
                return "I have a proposal that may benefit us both.";
            case PlayerPartyInteractionProposal.JoinClan:
                return "I wish to offer my services in your clan.";
            case PlayerPartyInteractionProposal.Vassal:
                return "I wish to swear my allegiance to your majesty.";
            case PlayerPartyInteractionProposal.Mercenary:
                var mercenaryProposalText = new TextObject("{=coop_player_party_mercenary_proposal}{OTHER_NAME} offers to serve as a mercenary. The kingdom will pay {MERCENARY_AWARD}{GOLD_ICON} gold per influence point earned, whenever the contract is honored. Do you accept?");
                mercenaryProposalText.SetTextVariable("OTHER_NAME", OtherPlayerName);
                mercenaryProposalText.SetTextVariable("MERCENARY_AWARD", MercenaryAwardMultiplier);
                return mercenaryProposalText.ToString();
            case PlayerPartyInteractionProposal.HostileDemand:
                return "I offer you one chance to surrender or die";
            case PlayerPartyInteractionProposal.PatrilinealMarriage:
                return GameTexts.FindText("str_coop_marriage_patrilineal_request").ToString();
            case PlayerPartyInteractionProposal.MatrilinealMarriage:
                return GameTexts.FindText("str_coop_marriage_matrilineal_request").ToString();
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
           option == PlayerPartyInteractionOption.Mercenary ||
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
