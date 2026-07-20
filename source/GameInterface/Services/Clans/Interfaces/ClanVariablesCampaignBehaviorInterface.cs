using Common.Messaging;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.UI.Notifications.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Clans.Interfaces;

public interface IClanVariablesCampaignBehaviorInterface : IGameAbstraction
{
    void DailyTickClan(ClanVariablesCampaignBehavior behavior, Clan clan);
}

public class ClanVariablesCampaignBehaviorInterface : IClanVariablesCampaignBehaviorInterface
{
    private readonly IMessageBroker messageBroker;

    public ClanVariablesCampaignBehaviorInterface(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
    }

    public void DailyTickClan(ClanVariablesCampaignBehavior behavior, Clan clan)
    {
        if (!clan.IsBanditFaction)
        {
            if (clan.Kingdom != null)
            {
                // Replace Clan.PlayerClan usage
                if (!clan.IsPlayerClan() && clan.IsUnderMercenaryService && clan.Kingdom != null && !clan.Kingdom.Leader.IsPlayerHero() && MBRandom.RandomFloat < 0.1f)
                {
                    clan.MercenaryAwardMultiplier = Campaign.Current.Models.MinorFactionsModel.GetMercenaryAwardFactorToJoinKingdom(clan, clan.Kingdom, false);
                }
                // Replace Clan.PlayerClan usage
                if (clan.IsPlayerClan() && clan.IsUnderMercenaryService && clan.Kingdom != null && Campaign.CurrentTime > Campaign.Current.KingdomManager.PlayerMercenaryServiceNextRenewalDay)
                {
                    clan.MercenaryAwardMultiplier = Campaign.Current.Models.MinorFactionsModel.GetMercenaryAwardFactorToJoinKingdom(clan, clan.Kingdom, false);
                    Campaign.Current.KingdomManager.PlayerMercenaryServiceNextRenewalDay = Campaign.CurrentTime + 30f * (float)CampaignTime.HoursInDay;
                }
                // Replace Clan.PlayerClan usage
                if (!clan.IsPlayerClan() && clan.IsUnderMercenaryService && clan.Kingdom != null && clan.Kingdom.RulingClan.DebtToKingdom > 10000 && MBRandom.RandomFloat < 0.25f && clan.ShouldStayInKingdomUntil.IsPast)
                {
                    ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary(clan, true);
                }
            }
            // Replace Clan.PlayerClan usage
            // Cause of random desertions without it due to setting lord party wage limits for "AI" parties that are actually player parties
            if (!clan.IsPlayerClan())
            {
                behavior.MakeClanFinancialEvaluation(clan);
            }
            int num = MathF.Round(Campaign.Current.Models.ClanFinanceModel.CalculateClanGoldChange(clan, false, true, false).ResultNumber);
            GiveGoldAction.ApplyBetweenCharacters(null, clan.Leader, num, true);
            if (clan.MapFaction.Leader == clan.Leader && clan.Kingdom != null)
            {
                int num2 = (clan.Kingdom.KingdomBudgetWallet < 2000000) ? 1000 : 0;
                if ((float)clan.Kingdom.KingdomBudgetWallet < 1000000f && MBRandom.RandomFloat < (((float)clan.Kingdom.KingdomBudgetWallet < 100000f) ? 0.01f : 0.005f))
                {
                    float randomFloat = MBRandom.RandomFloat;
                    num2 = ((randomFloat < 0.1f) ? 400000 : ((randomFloat < 0.3f) ? 200000 : 100000));
                }
                clan.Kingdom.KingdomBudgetWallet += num2;
            }
            float resultNumber = Campaign.Current.Models.ClanPoliticsModel.CalculateInfluenceChange(clan, false).ResultNumber;
            ChangeClanInfluenceAction.Apply(clan, resultNumber);

            // Replace Clan.PlayerClan usage
            if (clan.IsPlayerClan())
            {
                // Notify players of their daily gold changes
                var message = new NotifyDailyGoldChange(clan, num);
                messageBroker.Publish(behavior, message);
            }
        }
        // Replace Clan.PlayerClan usage
        if (!clan.IsPlayerClan())
        {
            behavior.UpdateGovernorsOfClan(clan);
            behavior.UpdateClanSettlementsPaymentLimit(clan);
            behavior.UpdateClanSettlementAutoRecruitment(clan);
        }
    }
}