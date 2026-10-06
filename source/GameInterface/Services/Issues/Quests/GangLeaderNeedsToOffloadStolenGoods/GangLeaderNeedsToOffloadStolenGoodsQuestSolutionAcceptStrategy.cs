using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsToOffloadStolenGoods;

/// <summary>
/// The quest's amounts and prices come from item prices and the player's progress, which differ per
/// machine. The accepting player's values are forced onto the server's quest object.
/// </summary>
internal class GangLeaderNeedsToOffloadStolenGoodsQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private static readonly FieldInfo AmountField = AccessTools.Field(typeof(Quest), "_stolenTradeGoodAmount");
    private static readonly FieldInfo PriceField = AccessTools.Field(typeof(Quest), "_stolenTradeGoodPrice");
    private static readonly FieldInfo CounterOfferGoldField = AccessTools.Field(typeof(Quest), "_counterOfferGold");
    private static readonly FieldInfo RewardGoldField = AccessTools.Field(typeof(QuestBase), nameof(QuestBase.RewardGold));

    public byte[] Capture(IssueBase issue)
    {
        if (!(issue.IssueQuest is Quest quest))
        {
            return Array.Empty<byte>();
        }

        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(quest._stolenTradeGoodAmount);
                writer.Write(quest._stolenTradeGoodPrice);
                writer.Write(quest._counterOfferGold);
                writer.Write(quest.RewardGold);
                writer.Flush();

                return stream.ToArray();
            }
        }
    }

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (!(issue.IssueQuest is Quest quest) || captured.Length == 0)
        {
            return;
        }

        using (var stream = new MemoryStream(captured))
        {
            using (var reader = new BinaryReader(stream))
            {
                AmountField.SetValue(quest, reader.ReadInt32());
                PriceField.SetValue(quest, reader.ReadInt32());
                CounterOfferGoldField.SetValue(quest, reader.ReadInt32());
                RewardGoldField.SetValue(quest, reader.ReadInt32());
            }
        }
    }
}
