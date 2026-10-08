using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
// dear god, this is long
using Issue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsToOffloadStolenGoods;

internal class GangLeaderNeedsToOffloadStolenGoodsCreationCaptureStrategy : ICreationCaptureStrategy
{
    private static readonly FieldInfo StolenTradeGoodField = AccessTools.Field(typeof(Issue), nameof(Issue._randomForStolenTradeGood));

    private readonly IObjectManager objectManager;

    public GangLeaderNeedsToOffloadStolenGoodsCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        var gangLeaderIssue = (Issue)issue;

        return Write(
            gangLeaderIssue._randomForStolenTradeGood,
            IdOf(gangLeaderIssue._issueHideout),
            IdOf(gangLeaderIssue.CounterOfferHero));
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        Read(captured, out var stolenTradeGood, out var hideoutId, out _);

        objectManager.TryGetObjectWithLogging<Settlement>(hideoutId, out var hideout);

        var issue = new Issue(issueOwner, hideout);
        StolenTradeGoodField.SetValue(issue, stolenTradeGood);

        return issue;
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
        Read(captured, out _, out _, out var counterOfferHeroId);

        if (counterOfferHeroId.Length == 0 || !objectManager.TryGetObjectWithLogging<Hero>(counterOfferHeroId, out var counterOfferHero))
        {
            return;
        }

        ((Issue)issue).CounterOfferHero = counterOfferHero;
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;

        var settlement = issueOwner.CurrentSettlement;
        if (settlement == null || !settlement.IsTown)
        {
            return false;
        }

        var hideouts = Campaign.Current.AllHideouts;
        var hideout = hideouts.FirstOrDefault(candidate => candidate.IsInfested) ?? hideouts.FirstOrDefault();
        if (hideout == null)
        {
            return false;
        }

        var counterOfferHero = settlement.Notables.FirstOrDefault(notable => notable != issueOwner && notable.IsMerchant)
            ?? settlement.Notables.FirstOrDefault();

        captured = Write(
            MBRandom.RandomInt(0, Issue.PossibleStolenItems.Length),
            IdOf(hideout.Settlement),
            IdOf(counterOfferHero));

        return true;
    }

    private string IdOf(object obj)
    {
        if (obj == null)
        {
            return string.Empty;
        }

        objectManager.TryGetIdWithLogging(obj, out var id);
        return id ?? string.Empty;
    }

    private static byte[] Write(int stolenTradeGood, string hideoutId, string counterOfferHeroId)
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(stolenTradeGood);
                writer.Write(hideoutId);
                writer.Write(counterOfferHeroId);
                writer.Flush();

                return stream.ToArray();
            }
        }
    }

    private static void Read(byte[] captured, out int stolenTradeGood, out string hideoutId, out string counterOfferHeroId)
    {
        using (var stream = new MemoryStream(captured))
        {
            using (var reader = new BinaryReader(stream))
            {
                stolenTradeGood = reader.ReadInt32();
                hideoutId = reader.ReadString();
                counterOfferHeroId = reader.ReadString();
            }
        }
    }
}
