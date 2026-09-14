using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using System;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Clans;

public class CoopClanManagementVM : ClanManagementVM
{
    private readonly IClanFinance finance;
    private readonly IMessageBroker messages;

    public CoopClanManagementVM(Action onClose, Action<Hero> showHeroOnMap, Action<Hero> openParty,
        Action openBannerEditor, IClanFinance finance, IMessageBroker messages)
        : base(onClose, showHeroOnMap, openParty, openBannerEditor)
    {
        this.finance = finance;
        this.messages = messages;
        RefreshFinanceControls();
    }

    private Hero SelectedPlayer => ClanParties.CurrentSelectedParty?.Party?.LeaderHero;

    [DataSourceProperty]
    public bool IsPlayerPaymentVisible => finance?.IsNonLeaderMember(SelectedPlayer) == true &&
        SelectedPlayer.Clan == _clan;

    [DataSourceProperty]
    public bool CanSetPlayerPayment => IsPlayerPaymentVisible && Hero.MainHero == _clan.Leader;

    [DataSourceProperty]
    public bool CanSendTribute => finance?.IsNonLeaderMember(Hero.MainHero) == true;

    [DataSourceProperty]
    public string PlayerPaymentText => GameTexts.FindText("str_coop_clan_daily_payment_amount")
        .SetTextVariable("AMOUNT", finance?.GetSettings(SelectedPlayer)?.DailyPayment ?? 0).ToString();

    [DataSourceProperty]
    public string SetPlayerPaymentText => GameTexts.FindText("str_coop_clan_set_daily_payment").ToString();

    [DataSourceProperty]
    public string SendTributeText => GameTexts.FindText("str_coop_clan_send_tribute").ToString();

    public void ExecuteSetPlayerPayment()
    {
        if (CanSetPlayerPayment) ShowAmountInquiry(SelectedPlayer, false);
    }

    public void ExecuteSendTribute()
    {
        if (CanSendTribute) ShowAmountInquiry(Hero.MainHero, true);
    }

    private void ShowAmountInquiry(Hero member, bool tribute)
    {
        var actor = Hero.MainHero;
        var clan = member.Clan;
        InformationManager.ShowTextInquiry(new TextInquiryData(
            GameTexts.FindText(tribute ? "str_coop_clan_send_tribute" : "str_coop_clan_daily_payment").ToString(),
            GameTexts.FindText("str_coop_clan_experimental_warning") + "\n\n" +
                GameTexts.FindText(tribute ? "str_coop_clan_tribute_description" : "str_coop_clan_payment_description")
                .SetTextVariable("HERO", tribute ? clan.Leader.Name : member.Name)
                .SetTextVariable("GOLD", actor.Gold).ToString(),
            true, true, GameTexts.FindText(tribute ? "str_coop_clan_send_tribute" : "str_done").ToString(),
            GameTexts.FindText("str_cancel").ToString(),
            text =>
            {
                if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 0)
                    messages.Publish(this, new ClanFinanceChangeRequested(actor, member, clan, value, tribute));
            }, null, textCondition: text => ValidateAmount(text, tribute ? 1 : 0, tribute ? actor.Gold : int.MaxValue),
            defaultInputText: tribute ? string.Empty : (finance.GetSettings(member)?.DailyPayment ?? 0).ToString(CultureInfo.InvariantCulture)));
    }

    public static Tuple<bool, string> ValidateAmount(string text, int minimum = 0, int maximum = int.MaxValue)
    {
        if (maximum < minimum)
            return Tuple.Create(false, GameTexts.FindText("str_coop_clan_no_tribute_gold").ToString());
        bool valid = int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var amount) &&
            amount >= minimum && amount <= maximum;
        return Tuple.Create(valid, valid ? string.Empty : GameTexts.FindText("str_coop_clan_invalid_payment")
            .SetTextVariable("MIN", minimum).SetTextVariable("MAX", maximum).ToString());
    }

    public void RefreshFinanceControls()
    {
        OnPropertyChanged(nameof(IsPlayerPaymentVisible));
        OnPropertyChanged(nameof(CanSetPlayerPayment));
        OnPropertyChanged(nameof(CanSendTribute));
        OnPropertyChanged(nameof(PlayerPaymentText));
        OnPropertyChanged(nameof(SetPlayerPaymentText));
        OnPropertyChanged(nameof(SendTributeText));
    }
}
