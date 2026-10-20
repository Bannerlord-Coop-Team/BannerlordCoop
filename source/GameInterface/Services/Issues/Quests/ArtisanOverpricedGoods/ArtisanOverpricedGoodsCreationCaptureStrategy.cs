using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using Helpers;
using System;
using System.IO;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using Issue = TaleWorlds.CampaignSystem.Issues.ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;

namespace GameInterface.Services.Issues.Quests.ArtisanOverpricedGoods;

internal class ArtisanOverpricedGoodsCreationCaptureStrategy : ICreationCaptureStrategy
{
    private readonly IObjectManager objectManager;

    public ArtisanOverpricedGoodsCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issueBase)
    {
        var issue = (Issue)issueBase;
        if (!objectManager.TryGetIdWithLogging(issue.CounterOfferHero, out var merchantId) ||
            !objectManager.TryGetIdWithLogging(issue._requestedTradeGood, out var itemId))
        {
            throw new InvalidOperationException("Artisan issue has an unregistered merchant or material");
        }

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(merchantId);
            writer.Write(itemId);
            writer.Write(issue.RequestedTradeGoodAmount);
            writer.Write(issue._goldReward);
            return stream.ToArray();
        }
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        using (var reader = new BinaryReader(new MemoryStream(captured)))
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(reader.ReadString(), out var merchant) ||
                !objectManager.TryGetObjectWithLogging<ItemObject>(reader.ReadString(), out var item))
            {
                throw new InvalidOperationException("Cannot resolve the artisan issue's merchant or material");
            }

            return new Issue(issueOwner, merchant, item)
            {
                RequestedTradeGoodAmount = reader.ReadInt32(),
                _goldReward = reader.ReadInt32(),
            };
        }
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;
        var settlement = issueOwner.CurrentSettlement;
        if (!issueOwner.IsArtisan || settlement?.IsTown != true)
        {
            return false;
        }

        var merchant = settlement.Notables.FirstOrDefault(hero => hero != issueOwner && hero.IsMerchant &&
            hero.IsActive && hero.GetTraitLevel(DefaultTraits.Mercy) <= 0);
        if (merchant == null)
        {
            return false;
        }

        var item = DefaultItems.HardWood;
        var difficulty = Campaign.Current.Models.IssueModel.GetIssueDifficultyMultiplier();
        var amount = Math.Max((int)(10000f / item.Value * difficulty), 1);
        var reward = (int)(QuestHelper.GetAveragePriceOfItemInTheWorld(item) * 1.5f * amount);
        if (!objectManager.TryGetIdWithLogging(merchant, out var merchantId) ||
            !objectManager.TryGetIdWithLogging(item, out var itemId))
        {
            return false;
        }

        captured = Write(merchantId, itemId, amount, reward);
        return true;
    }

    private static byte[] Write(string merchantId, string itemId, int amount, int reward)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(merchantId);
        writer.Write(itemId);
        writer.Write(amount);
        writer.Write(reward);
        return stream.ToArray();
    }
}
