using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System.IO;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Issue = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;
using Behavior = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts;

internal class ArtisanProductsCreationCaptureStrategy : ICreationCaptureStrategy
{
    private readonly IObjectManager objects;
    private (Hero Owner, Settlement Target, Hero Merchant)? creating;

    public ArtisanProductsCreationCaptureStrategy(IObjectManager objects)
    {
        this.objects = objects;
    }

    public byte[] Capture(IssueBase issue)
    {
        var artisan = (Issue)issue;
        return Write(artisan._targetSettlement, artisan._targetHero, artisan._rawMaterialsToBeDelivered, artisan.CounterOfferHero);
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        using (var reader = new BinaryReader(new MemoryStream(captured)))
        {
            if (!objects.TryGetObjectWithLogging<Settlement>(reader.ReadString(), out var settlement) ||
                !objects.TryGetObjectWithLogging<Hero>(reader.ReadString(), out var target) ||
                !objects.TryGetObjectWithLogging<ItemObject>(reader.ReadString(), out var item) ||
                !objects.TryGetObjectWithLogging<Hero>(reader.ReadString(), out var merchant))
                throw new InvalidDataException("Artisan issue references an unknown object");

            var previous = creating;
            creating = (issueOwner, settlement, merchant);
            try
            {
                // An existing local quest can hide a target from vanilla's constructor selection.
                var issue = new Issue(issueOwner);
                issue._targetSettlement = settlement;
                issue._targetHero = target;
                issue._rawMaterialsToBeDelivered = item;
                issue.CounterOfferHero = merchant;
                return issue;
            }
            finally { creating = previous; }
        }
    }

    internal bool TryGetCreation(Hero owner, out Settlement target, out Hero merchant)
    {
        target = creating?.Target;
        merchant = creating?.Merchant;
        return creating?.Owner == owner && target != null;
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured) { }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;
        if (!issueOwner.IsArtisan || issueOwner.CurrentSettlement?.IsTown != true) return false;
        var destination = SelectTargetSettlement(issueOwner);
        var target = destination?.Notables.FirstOrDefault(hero => hero.CanHaveCampaignIssues());
        var merchant = Behavior.SelectCounterOfferHero(issueOwner);
        if (target == null || merchant == null) return false;
        captured = Write(destination, target, DefaultItems.HardWood, merchant);
        return true;
    }

    internal static Settlement SelectTargetSettlement(Hero giver)
    {
        var origin = giver.CurrentSettlement;
        var navigation = origin.OwnerClan.HasNavalNavigationCapability ? MobileParty.NavigationType.All : MobileParty.NavigationType.Default;
        var maximum = Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(navigation) * 2.25f;
        return Settlement.All.Where(town => town.IsTown && town != origin && town.Notables.Any(hero => hero.CanHaveCampaignIssues()))
            .Select(town => new { Town = town, Distance = Campaign.Current.Models.MapDistanceModel.GetDistance(town, origin, false, false, navigation) })
            .Where(candidate => candidate.Distance < maximum)
            .OrderBy(candidate => candidate.Distance).ThenBy(candidate => candidate.Town.StringId)
            .Select(candidate => candidate.Town).FirstOrDefault();
    }

    private byte[] Write(Settlement settlement, Hero target, ItemObject item, Hero merchant)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            foreach (var obj in new object[] { settlement, target, item, merchant })
            {
                if (!objects.TryGetIdWithLogging(obj, out var id)) throw new InvalidDataException("Artisan issue object has no id");
                writer.Write(id);
            }
            return stream.ToArray();
        }
    }
}
