using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Interfaces;

public interface IDefaultClanFinanceModelInterface : IGameAbstraction
{
    int AddExpenseFromLeaderParty(DefaultClanFinanceModel model, Clan clan, ExplainedNumber goldChange, bool applyWithdrawals);
    void CalculateClanIncomeInternal(DefaultClanFinanceModel model, Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals = false, bool includeDetails = false);
    void CalculateClanExpensesForPlayerClan(DefaultClanFinanceModel __instance, Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals = false, bool includeDetails = false);
    int AddPartyExpense(DefaultClanFinanceModel __instance, MobileParty party, Clan clan, ExplainedNumber goldChange, bool applyWithdrawals);
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
        if (clan.Gold < 30000 && clan.Kingdom != null && !clan.Leader.IsPlayerHero() && !clan.IsUnderMercenaryService)
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

    public void CalculateClanExpensesForPlayerClan(DefaultClanFinanceModel __instance, Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals = false, bool includeDetails = false)
    {
        __instance.AddExpensesFromPartiesAndGarrisons(clan, ref goldChange, applyWithdrawals, includeDetails);
        if (!clan.IsUnderMercenaryService)
        {
            __instance.AddExpensesForHiredMercenaries(clan, ref goldChange, applyWithdrawals);
            __instance.AddExpensesForTributes(clan, ref goldChange, applyWithdrawals);
        }
        __instance.AddExpensesForAutoRecruitment(clan, ref goldChange, applyWithdrawals);
        if (clan.DebtToKingdom > 0)
        {
            __instance.AddPaymentForDebts(clan, ref goldChange, applyWithdrawals);
        }
        AddExpenseForPlayerWorkshops(clan, ref goldChange);
        if (!clan.IsUnderMercenaryService)
        {
            __instance.AddExpensesForCallToWarAgreements(clan, ref goldChange, applyWithdrawals);
        }
    }

    public int AddPartyExpense(DefaultClanFinanceModel __instance, MobileParty party, Clan clan, ExplainedNumber goldChange, bool applyWithdrawals)
    {
        int initialGoldAfterChange = clan.Gold + (int)goldChange.ResultNumber;
        int actualGoldAfterChange = initialGoldAfterChange;
        if (initialGoldAfterChange < (party.IsGarrison ? 8000 : 4000) && applyWithdrawals && !clan.IsPlayerClan()) // Replace usage of clan != Clan.PlayerClan
        {
            actualGoldAfterChange = ((party.LeaderHero != null && party.PartyTradeGold < 500) ? MathF.Min(initialGoldAfterChange, 250) : 0);
        }
        int partyWage = __instance.CalculatePartyWage(party, actualGoldAfterChange, applyWithdrawals);

        int partyTradeGold = party.PartyTradeGold;
        if (applyWithdrawals)
        {
            if (party.IsLordParty && party.LeaderHero == null)
            {
                party.ActualClan.Leader.Gold -= partyWage;
            }
            else
            {
                party.PartyTradeGold -= partyWage;
            }
        }
        partyTradeGold -= partyWage;
        if (partyTradeGold < __instance.PartyGoldLowerThreshold)
        {
            int wage = __instance.PartyGoldLowerThreshold - partyTradeGold;
            if (party.IsLordParty && party.LeaderHero == null)
            {
                wage = partyWage;
            }
            if (applyWithdrawals)
            {
                wage = MathF.Min(wage, actualGoldAfterChange);
                party.PartyTradeGold += wage;
            }
            return -wage;
        }
        return 0;
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

    private void AddExpenseForPlayerWorkshops(Clan clan, ref ExplainedNumber goldChange)
    {
        foreach (var hero in clan.Heroes)
        {
            if (!hero.IsAlive) continue;

            int expense = 0;
            foreach (Workshop workshop in hero.OwnedWorkshops)
            {
                if (workshop.Capital < Campaign.Current.Models.WorkshopModel.CapitalLowLimit)
                {
                    expense -= workshop.Expense;
                }
            }
            goldChange.Add((float)expense, DefaultClanFinanceModel._shopExpenseText, null);
        }
    }
}