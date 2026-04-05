using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Kingdoms;

public class KingdomSyncTests : SyncTestBase
{
    private string KingdomId;

    public KingdomSyncTests(ITestOutputHelper output) : base(output)
    {
        KingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        TestEnvironment.CreateRegisteredObject<Settlement>();
        TestEnvironment.CreateRegisteredObject<Clan>();
        TestEnvironment.CreateRegisteredObject<CultureObject>();
    }

    [Fact] 
    public void Server_Kingdom_Fields()
    {
        // PoliticalStagnation is randomly initialized in the Kingdom constructor
        Server.ObjectManager.TryGetObject<Kingdom>(KingdomId, out var kingdom);
        var initialPoliticalStagnation = kingdom.PoliticalStagnation;

        //TestEnvironment.AssertReferenceField<Kingdom, Settlement>(nameof(Kingdom._kingdomMidSettlement));
        TestEnvironment.AssertReferenceField<Kingdom, Clan>(nameof(Kingdom._rulingClan));
        TestEnvironment.AssertField<Kingdom, int>(nameof(Kingdom.PoliticalStagnation), 5, defaultValue: initialPoliticalStagnation);
        TestEnvironment.AssertField<Kingdom, float>(nameof(Kingdom._aggressiveness), 5f);
        TestEnvironment.AssertField<Kingdom, bool>(nameof(Kingdom._isEliminated), true);
        TestEnvironment.AssertField<Kingdom, int>(nameof(Kingdom._kingdomBudgetWallet), 5);
        TestEnvironment.AssertField<Kingdom, int>(nameof(Kingdom._tributeWallet), 5);
        TestEnvironment.AssertField<Kingdom, bool>(nameof(Kingdom._distanceToClosestNonAllyFortificationCacheDirty), false, defaultValue: true);
    }

    [Fact]
    public void Server_Kingdom_Properties()
    {
        var banner = new Banner();
        banner.BannerDataList.Add(new BannerData(1, 2, 3, Vec2.One, Vec2.Zero, true, true, 0f));

        Server.ObjectManager.TryGetObject(KingdomId, out Kingdom kingdom);

        //TestEnvironment.AssertProperty<Kingdom, uint>(nameof(Kingdom.AlternativeColor), 7);
        //TestEnvironment.AssertProperty<Kingdom, uint>(nameof(Kingdom.AlternativeColor2), 7);
        TestEnvironment.AssertProperty<Kingdom, Banner>(nameof(Kingdom.Banner), banner);
        TestEnvironment.AssertProperty<Kingdom, uint>(nameof(Kingdom.Color), 7);
        TestEnvironment.AssertProperty<Kingdom, uint>(nameof(Kingdom.Color2), 7);
        TestEnvironment.AssertReferenceProperty<Kingdom, CultureObject>(nameof(Kingdom.Culture));
        TestEnvironment.AssertProperty<Kingdom, TextObject>(nameof(Kingdom.EncyclopediaRulerTitle), new TextObject("ruler"), kingdom.EncyclopediaRulerTitle);
        TestEnvironment.AssertProperty<Kingdom, TextObject>(nameof(Kingdom.EncyclopediaText), new TextObject("encycText"), kingdom.EncyclopediaText);
        TestEnvironment.AssertProperty<Kingdom, TextObject>(nameof(Kingdom.EncyclopediaTitle), new TextObject("encycTitle"), kingdom.EncyclopediaTitle);
        TestEnvironment.AssertProperty<Kingdom, TextObject>(nameof(Kingdom.InformalName), new TextObject("informalName"), kingdom.InformalName);
        //TestEnvironment.AssertReferenceProperty<Kingdom, Settlement>(nameof(Kingdom.InitialHomeLand));
        //TestEnvironment.AssertProperty<Kingdom, uint>(nameof(Kingdom.LabelColor), 7);
        TestEnvironment.AssertProperty<Kingdom, int>(nameof(Kingdom.LastArmyCreationDay), 7);
        TestEnvironment.AssertProperty<Kingdom, CampaignTime>(nameof(Kingdom.LastKingdomDecisionConclusionDate), new CampaignTime(54));
        TestEnvironment.AssertProperty<Kingdom, CampaignTime>(nameof(Kingdom.LastMercenaryOfferTime), new CampaignTime(54));
        TestEnvironment.AssertProperty<Kingdom, float>(nameof(Kingdom.MainHeroCrimeRating), 55f);
        TestEnvironment.AssertProperty<Kingdom, TextObject>(nameof(Kingdom.Name), new TextObject("kingdomName"), kingdom.Name);
        TestEnvironment.AssertProperty<Kingdom, CampaignTime>(nameof(Kingdom.NotAttackableByPlayerUntilTime), new CampaignTime(54));
        TestEnvironment.AssertProperty<Kingdom, uint>(nameof(Kingdom.PrimaryBannerColor), 7);
        TestEnvironment.AssertProperty<Kingdom, uint>(nameof(Kingdom.SecondaryBannerColor), 7);
    }
}