using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts;

internal class ArtisanProductsQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private readonly IIssueOwnerResolver owners;
    private static readonly FieldInfo Amount = AccessTools.Field(typeof(Quest), nameof(Quest._amountOfRawGoodsToBeDelivered));
    private static readonly FieldInfo Reward = AccessTools.Field(typeof(QuestBase), nameof(QuestBase.RewardGold));

    public ArtisanProductsQuestSolutionAcceptStrategy(IIssueOwnerResolver owners)
    {
        this.owners = owners;
    }
    public byte[] Capture(IssueBase issue)
    {
        if (!(issue.IssueQuest is Quest quest)) return Array.Empty<byte>();
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(quest._amountOfRawGoodsToBeDelivered);
            writer.Write(quest._rewardGold);
            writer.Write(quest.QuestDueTime.NumTicks);
            return stream.ToArray();
        }
    }

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (!(issue.IssueQuest is Quest quest)) return;
        using (var reader = new BinaryReader(new MemoryStream(captured)))
        {
            var amount = reader.ReadInt32();
            var reward = reader.ReadInt32();
            var dueTime = reader.ReadInt64();
            // Publicizing leaves readonly fields readonly.
            Amount.SetValue(quest, amount);
            Reward.SetValue(quest, reward);
            quest._rewardGold = reward;
            quest.QuestDueTime = new CampaignTime(dueTime);
        }

        if (ModInformation.IsServer)
        {
            // The server never starts its quest, but partial hand-ins need its task log.
            quest._playerStartsQuestLog = quest.AddDiscreteLog(quest.PlayerStartsQuestLogText,
                new TextObject("{=L700FNht}Delivered {RAW_MATERIAL}").SetTextVariable("RAW_MATERIAL", quest._rawMaterialsToBeDelivered.Name),
                0, quest._amountOfRawGoodsToBeDelivered, null, true);
            if (!owners.TryResolveOwnerHero(issue, out var hero) || hero.PartyBelongedTo == null)
                throw new InvalidOperationException("Artisan quest owner has no party");
            hero.PartyBelongedTo.ItemRoster.AddToCounts(quest._rawMaterialsToBeDelivered, quest._amountOfRawGoodsToBeDelivered);
        }
    }
}
