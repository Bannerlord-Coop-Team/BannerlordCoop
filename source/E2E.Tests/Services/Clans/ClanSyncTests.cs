using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Clans
{
    public class ClanSyncTests : SyncTestBase
    {

        private string KingdomId;
        private string SettlementId;
        private string CharacterObjectId;
        private string HeroId;
        private string CultureId;
        private string ClanId;

        public ClanSyncTests(ITestOutputHelper output) : base(output)
        {
            ClanId = TestEnvironment.CreateRegisteredObject<Clan>();
            KingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
            SettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
            CharacterObjectId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
            HeroId = TestEnvironment.CreateRegisteredObject<Hero>();
            CultureId = TestEnvironment.CreateRegisteredObject<CultureObject>();
        }

        [Fact]
        public void Server_Clan_Fields()
        {
            var banner = new Banner();
            banner.BannerDataList.Add(new BannerData(1, 2, 3, Vec2.One, Vec2.Zero, true, true, 0f));
            TestEnvironment.AssertField<Clan, bool>(nameof(Clan._isEliminated), true);
            TestEnvironment.AssertReferenceField<Clan, Kingdom>(nameof(Clan._kingdom));
            TestEnvironment.AssertField<Clan, float>(nameof(Clan._influence), 0.5f);
            TestEnvironment.AssertReferenceField<Clan,Settlement>(nameof(Clan._clanMidSettlement));
            TestEnvironment.AssertReferenceField<Clan, CharacterObject>(nameof(Clan._basicTroop));
            TestEnvironment.AssertReferenceField<Clan, Hero>(nameof(Clan._leader));
            TestEnvironment.AssertField<Clan, Banner>(nameof(Clan._banner), banner);
            TestEnvironment.AssertField<Clan, int>(nameof(Clan._tier), 2);
            TestEnvironment.AssertField<Clan, float>(nameof(Clan._aggressiveness), 0.8f);
            TestEnvironment.AssertField<Clan, int>(nameof(Clan._tributeWallet), 20000);
            TestEnvironment.AssertReferenceField<Clan, Settlement>(nameof(Clan._home));
            TestEnvironment.AssertField<Clan, int>(nameof(Clan._clanDebtToKingdom), 5000);
        }

        [Fact]
        public void Server_Clan_Properties()
        {
            // Arrange

            // Assert
            Server.ObjectManager.TryGetObject(ClanId, out Clan clan);
            TestEnvironment.AssertProperty<Clan, TextObject>(nameof(Clan.Name), new TextObject("new clan"), clan.Name);
            TestEnvironment.AssertProperty<Clan, TextObject>(nameof(Clan.InformalName), new TextObject("new clan informational"), clan.InformalName);
            TestEnvironment.AssertReferenceProperty<Clan, CultureObject>(nameof(Clan.Culture));
            TestEnvironment.AssertProperty<Clan, CampaignTime>(nameof(Clan.LastFactionChangeTime), new CampaignTime(12341));
            TestEnvironment.AssertProperty<Clan, int>(nameof(Clan.AutoRecruitmentExpenses), 20);
            TestEnvironment.AssertProperty<Clan, bool>(nameof(Clan.IsNoble), true);
            TestEnvironment.AssertProperty<Clan, float>(nameof(Clan.TotalStrength), 200f);
            TestEnvironment.AssertProperty<Clan, int>(nameof(Clan.MercenaryAwardMultiplier), 20);
            TestEnvironment.AssertProperty<Clan, uint>(nameof(Clan.LabelColor), 123);
            TestEnvironment.AssertProperty<Clan, Vec2>(nameof(Clan.InitialPosition),new Vec2(2f,4f));
            TestEnvironment.AssertProperty<Clan, bool>(nameof(Clan.IsRebelClan), true);
            TestEnvironment.AssertProperty<Clan, bool>(nameof(Clan.IsUnderMercenaryService), true);
            TestEnvironment.AssertProperty<Clan, uint>(nameof(Clan.Color),321);
            TestEnvironment.AssertProperty<Clan, uint>(nameof(Clan.Color2),432);
            TestEnvironment.AssertProperty<Clan, uint>(nameof(Clan.BannerBackgroundColorPrimary),543);
            TestEnvironment.AssertProperty<Clan, uint>(nameof(Clan.BannerBackgroundColorSecondary), 654);
            TestEnvironment.AssertProperty<Clan, uint>(nameof(Clan.BannerIconColor),765);
            TestEnvironment.AssertProperty<Clan, bool>(nameof(Clan._midPointCalculated), true);
            TestEnvironment.AssertProperty<Clan, float>(nameof(Clan.Renown), 20f);
            TestEnvironment.AssertProperty<Clan, CampaignTime>(nameof(Clan.NotAttackableByPlayerUntilTime), new CampaignTime(7644567));
        }
    }
}