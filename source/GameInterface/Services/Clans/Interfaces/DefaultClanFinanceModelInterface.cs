using GameInterface.Services.Clans.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Interfaces;

public interface IDefaultClanFinanceModelInterface : IGameAbstraction
{
    int AddExpenseFromLeaderParty(DefaultClanFinanceModel model, Clan clan, ExplainedNumber goldChange, bool applyWithdrawals);
    void CalculateClanIncomeInternal(DefaultClanFinanceModel model, Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals = false, bool includeDetails = false);
}

public class DefaultClanFinanceModelInterface : IDefaultClanFinanceModelInterface
{
    private static readonly TextObject AlleyIncomeText = new TextObject("{=coop_alley_income}Alleys");

    public int AddExpenseFromLeaderParty(DefaultClanFinanceModel model, Clan clan, ExplainedNumber goldChange, bool applyWithdrawals)
    {
        if (clan == null) return 0;

        Hero leader = clan.Leader;
        MobileParty mobileParty = (leader != null) ? leader.PartyBelongedTo : null;
        if (mobileParty != null)
        {
            int num = clan.Gold + (int)goldChange.ResultNumber;
            if (num < 2000 && applyWithdrawals && !clan.IsPlayerClan()) // Vanilla runs clan != Clan.PlayerClan, which is always true on server
            {
                num = 0;
            }
            return -model.CalculatePartyWage(mobileParty, num, applyWithdrawals);
        }
        return 0;
    }

    public void CalculateClanIncomeInternal(DefaultClanFinanceModel model, Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals = false, bool includeDetails = false)
    {
        if (clan.IsEliminated) return;

        Kingdom kingdom = clan.Kingdom;
        if (((kingdom != null) ? kingdom.RulingClan : null) == clan)
        {
            model.AddRulingClanIncome(clan, ref goldChange, applyWithdrawals, includeDetails);
        }

        // Replace Clan.PlayerClan check to prevent adding extra money to player clans
        if (!clan.IsPlayerClan() && (!clan.MapFaction.IsKingdomFaction || clan.IsUnderMercenaryService) && clan.Fiefs.Count == 0)
        {
            int num = clan.Tier * (80 + (clan.IsUnderMercenaryService ? 40 : 0));
            goldChange.Add((float)num, null, null);
        }
        model.AddMercenaryIncome(clan, ref goldChange, applyWithdrawals);
        model.AddSettlementIncome(clan, ref goldChange, applyWithdrawals, includeDetails);
        model.CalculateHeroIncomeFromWorkshops(clan.Leader, ref goldChange, applyWithdrawals);
        model.AddIncomeFromParties(clan, ref goldChange, applyWithdrawals, includeDetails);
        if (clan.IsPlayerClan())
        {
            AddIncomeFromOwnedAlleys(clan, ref goldChange);
        }
        if (!clan.IsUnderMercenaryService)
        {
            model.AddIncomeFromTribute(clan, ref goldChange, applyWithdrawals, includeDetails);
            model.AddIncomeFromCallToWarAgrements(clan, ref goldChange, applyWithdrawals);
            if (clan.Kingdom != null && model.TradeAgreementsBehavior != null)
            {
                model.AddIncomeFromTradeAgreements(clan, ref goldChange, applyWithdrawals, includeDetails);
            }
        }
        if (clan.Gold < 30000 && clan.Kingdom != null && clan.Leader != Hero.MainHero && !clan.IsUnderMercenaryService)
        {
            model.AddIncomeFromKingdomBudget(clan, ref goldChange, applyWithdrawals);
        }
        Hero leader = clan.Leader;
        if (leader != null && leader.GetPerkValue(DefaultPerks.Trade.SpringOfGold))
        {
            int num2 = MathF.Min(1000, MathF.Round((float)clan.Leader.Gold * DefaultPerks.Trade.SpringOfGold.PrimaryBonus));
            goldChange.Add((float)num2, DefaultPerks.Trade.SpringOfGold.Name, null);
        }
    }

    private void AddIncomeFromOwnedAlleys(Clan clan, ref ExplainedNumber goldChange)
    {
        int income = 0;
        foreach (Hero hero in clan.Heroes)
        {
            if (!hero.IsAlive) continue;

            foreach (Alley alley in hero.OwnedAlleys)
            {
                income += Campaign.Current.Models.AlleyModel.GetDailyIncomeOfAlley(alley);
            }
        }

        if (income != 0)
        {
            goldChange.Add(income, AlleyIncomeText);
        }
    }
}