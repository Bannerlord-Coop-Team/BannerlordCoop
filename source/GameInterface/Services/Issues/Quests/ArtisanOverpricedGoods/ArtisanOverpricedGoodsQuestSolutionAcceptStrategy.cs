using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArtisanOverpricedGoods;

internal class ArtisanOverpricedGoodsQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    // Publicizer preserves readonly fields, so the mirror's constructor amount needs reflection.
    private static readonly FieldInfo AmountField = AccessTools.Field(typeof(Quest), nameof(Quest._requestedTradeGoodAmount));

    public byte[] Capture(IssueBase issue)
    {
        if (!(issue.IssueQuest is Quest quest)) return Array.Empty<byte>();

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(quest._requestedTradeGoodAmount);
            writer.Write(quest._rewardGold);
            return stream.ToArray();
        }
    }

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (!(issue.IssueQuest is Quest quest) || captured.Length == 0) return;

        using (var reader = new BinaryReader(new MemoryStream(captured)))
        {
            AmountField.SetValue(quest, reader.ReadInt32());
            quest._rewardGold = reader.ReadInt32();
        }
    }
}
