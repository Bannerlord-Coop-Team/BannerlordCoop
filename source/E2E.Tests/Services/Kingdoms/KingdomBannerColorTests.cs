using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Banners.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Kingdoms;

public class KingdomBannerColorTests : SyncTestBase
{
    private const int BackgroundColorId = 9001;
    private const int OldIconColorId = 9002;
    private const int NewIconColorId = 9003;
    private const uint BackgroundColor = 0xFF102030;
    private const uint OldIconColor = 0xFF405060;
    private const uint NewIconColor = 0xFF708090;

    private const string OriginalBannerCode = "11.9001.9001.1528.1528.764.764.1.0.0.133.9002.9002.512.512.764.764.0.0.0";
    private const string EditedBannerCode = "11.9001.9001.1528.1528.764.764.1.0.0.133.9003.9003.512.512.764.764.0.0.0";

    private readonly string kingdomId;
    private readonly string rulerClanId;
    private readonly string memberClanId;

    public KingdomBannerColorTests(ITestOutputHelper output) : base(output)
    {
        kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        rulerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        memberClanId = TestEnvironment.CreateRegisteredObject<Clan>();
    }

    [Fact]
    public void RulerBannerEdit_SurvivesKingdomRepaintOnLoad()
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() => SetUpKingdom(instance));
        }

        Server.SimulateMessage(this, new NetworkUpdatePlayerBanner(EditedBannerCode, rulerClanId, BackgroundColor, NewIconColor));
        Server.PumpGameThread();
        foreach (var client in Clients)
        {
            client.PumpGameThread();
        }

        Server.Call(() =>
        {
            var kingdom = Server.GetRegisteredObject<Kingdom>(kingdomId);
            var rulerClan = Server.GetRegisteredObject<Clan>(rulerClanId);

            Assert.Equal(BackgroundColor, kingdom.PrimaryBannerColor);
            Assert.Equal(NewIconColor, kingdom.SecondaryBannerColor);

            // Clan.PreAfterLoad runs this on every save load.
            rulerClan.UpdateBannerColorsAccordingToKingdom();

            Assert.Equal(EditedBannerCode, rulerClan.Banner.Serialize());
        });

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var kingdom = instance.GetRegisteredObject<Kingdom>(kingdomId);
                var memberClan = instance.GetRegisteredObject<Clan>(memberClanId);

                Assert.Equal(BackgroundColor, kingdom.PrimaryBannerColor);
                Assert.Equal(NewIconColor, kingdom.SecondaryBannerColor);
                Assert.Equal(EditedBannerCode, memberClan.Banner.Serialize());
            });
        }
    }

    private void SetUpKingdom(EnvironmentInstance instance)
    {
        BannerManager.Initialize();
        var palette = BannerManager.Instance._colorPalette;
        palette[BackgroundColorId] = new BannerColor(BackgroundColor, true, true);
        palette[OldIconColorId] = new BannerColor(OldIconColor, true, true);
        palette[NewIconColorId] = new BannerColor(NewIconColor, true, true);
        BannerManager.Instance.ReadOnlyColorPalette = palette.GetReadOnlyDictionary();

        var kingdom = instance.GetRegisteredObject<Kingdom>(kingdomId);
        var rulerClan = instance.GetRegisteredObject<Clan>(rulerClanId);
        var memberClan = instance.GetRegisteredObject<Clan>(memberClanId);

        using (new AllowedThread())
        {
            kingdom.Banner = new Banner(OriginalBannerCode);
            kingdom.PrimaryBannerColor = BackgroundColor;
            kingdom.SecondaryBannerColor = OldIconColor;
            kingdom._rulingClan = rulerClan;
            kingdom._clans = new MBList<Clan> { rulerClan, memberClan };

            rulerClan._kingdom = kingdom;
            rulerClan._banner = new Banner(OriginalBannerCode);
            memberClan._kingdom = kingdom;
            memberClan._banner = new Banner(OriginalBannerCode);
        }
    }
}
