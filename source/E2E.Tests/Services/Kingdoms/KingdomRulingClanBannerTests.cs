using Common;
using Common.Util;
using E2E.Tests.Util;
using GameInterface.Services.Kingdoms.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Kingdoms;

public class KingdomRulingClanBannerTests : SyncTestBase
{
    private readonly string kingdomId;
    private readonly string outgoingRulerClanId;
    private readonly string incomingRulerClanId;

    public KingdomRulingClanBannerTests(ITestOutputHelper output) : base(output)
    {
        kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        outgoingRulerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        incomingRulerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
    }

    [Fact]
    public void Server_RulingClanChanged_DecouplesOutgoingClanBannerFromKingdomBanner()
    {
        const string originalBannerCode = "1.2.3.1528.1528.764.764.0.0.0";
        const string editedBannerCode = "2.3.4.1528.1528.764.764.0.0.0";

        Server.Call(() =>
        {
            var kingdom = Server.GetRegisteredObject<Kingdom>(kingdomId);
            var outgoingRulerClan = Server.GetRegisteredObject<Clan>(outgoingRulerClanId);
            var incomingRulerClan = Server.GetRegisteredObject<Clan>(incomingRulerClanId);
            var sharedBanner = new Banner(originalBannerCode);

            using (new AllowedThread())
            {
                outgoingRulerClan._kingdom = kingdom;
                incomingRulerClan._kingdom = kingdom;
                outgoingRulerClan._banner = sharedBanner;
                kingdom.Banner = sharedBanner;
                kingdom._rulingClan = outgoingRulerClan;
            }

            Assert.Same(outgoingRulerClan.ClanOriginalBanner, kingdom.Banner);
        });

        Server.SimulateMessage(
            this,
            new NetworkRulingClanChanged(kingdomId, incomingRulerClanId));

        Server.Call(() =>
        {
            var kingdom = Server.GetRegisteredObject<Kingdom>(kingdomId);
            var outgoingRulerClan = Server.GetRegisteredObject<Clan>(outgoingRulerClanId);
            var incomingRulerClan = Server.GetRegisteredObject<Clan>(incomingRulerClanId);

            Assert.Same(incomingRulerClan, kingdom.RulingClan);
            Assert.Same(kingdom.Banner, incomingRulerClan.Banner);
            Assert.NotSame(outgoingRulerClan.ClanOriginalBanner, kingdom.Banner);
            Assert.Equal(originalBannerCode, kingdom.Banner.Serialize());
            Assert.Equal(originalBannerCode, outgoingRulerClan.ClanOriginalBanner.Serialize());

            kingdom.Banner.Deserialize(editedBannerCode);

            Assert.Equal(editedBannerCode, incomingRulerClan.Banner.Serialize());
            Assert.Equal(originalBannerCode, outgoingRulerClan.ClanOriginalBanner.Serialize());

            using (new AllowedThread())
            {
                outgoingRulerClan._kingdom = null;
            }

            Assert.Same(outgoingRulerClan.ClanOriginalBanner, outgoingRulerClan.Banner);
            Assert.NotSame(outgoingRulerClan.Banner, kingdom.Banner);
            Assert.Equal(originalBannerCode, outgoingRulerClan.Banner.Serialize());
        });
    }
}
