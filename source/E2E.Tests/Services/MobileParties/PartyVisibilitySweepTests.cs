using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.MobileParties;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;

/// <summary>
/// Verifies the post-join fog-of-war rebuild. A joining client loads the server's transferred save,
/// where every party is forced visible (PartyVisibilityServerPatches); the native load path never
/// recomputes visibility and the per-tick sweep only touches parties near the main party, so
/// <see cref="PartyVisibilitySweep"/> must hide everything beyond seeing range in one pass.
/// </summary>
public class PartyVisibilitySweepTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();

    private readonly bool wasForceAllVisible;

    public PartyVisibilitySweepTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        // Debug test builds default to force-all-visible; pin release-client behavior so the
        // sweep's writes reach the native setter unmodified.
        wasForceAllVisible = DebugPartyVisibility.ForceAllVisible;
        DebugPartyVisibility.ForceAllVisible = false;
    }

    public void Dispose()
    {
        DebugPartyVisibility.ForceAllVisible = wasForceAllVisible;
        TestEnvironment.Dispose();
    }

    [Fact]
    public void RebuildAroundMainParty_RestoresFogOfWar()
    {
        // Arrange
        string? mainPartyId = null;
        string? nearPartyId = null;
        string? farPartyId = null;

        Server.Call(() =>
        {
            var mainParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var nearParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var farParty = GameObjectCreator.CreateInitializedObject<MobileParty>();

            Assert.True(Server.ObjectManager.TryGetId(mainParty, out mainPartyId));
            Assert.True(Server.ObjectManager.TryGetId(nearParty, out nearPartyId));
            Assert.True(Server.ObjectManager.TryGetId(farParty, out farPartyId));
        });

        // Act + Assert
        Client.Call(() =>
        {
            EnsureMapVisibilityModel();

            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(mainPartyId, out var mainParty));
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(nearPartyId, out var nearParty));
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(farPartyId, out var farParty));

            // The sweep walks the campaign's party list, matching Campaign.GameInitTick.
            Assert.Contains(mainParty, Campaign.Current.MobileParties);
            Assert.Contains(nearParty, Campaign.Current.MobileParties);
            Assert.Contains(farParty, Campaign.Current.MobileParties);

            mainParty._position = LandPosition(100f, 100f);
            nearParty._position = LandPosition(130f, 100f);   // distance 30, inside seeing range 50
            farParty._position = LandPosition(600f, 100f);    // distance 500, far outside
            mainParty.IsActive = true;
            nearParty.IsActive = true;
            farParty.IsActive = true;
            Campaign.Current.MainParty = mainParty;

            // A joining client loads the server's save, where every party was forced visible.
            mainParty._isVisible = true;
            nearParty._isVisible = true;
            farParty._isVisible = true;

            PartyVisibilitySweep.RebuildAroundMainParty();

            Assert.True(mainParty.IsVisible);
            Assert.True(nearParty.IsVisible);
            Assert.False(farParty.IsVisible);
        });
    }

    /// <summary>
    /// The harness campaign boots without <c>Campaign.Models</c>; visibility only needs the
    /// map-visibility model. Fixed ranges make the assertions exact: seeing range 50 with spotting
    /// ratio 1 means a party is visible iff it is within distance 50 of the main party.
    /// </summary>
    private void EnsureMapVisibilityModel()
    {
        if (Campaign.Current.Models == null)
        {
            var gameModels = Client.GameInstance.Game.AddGameModelsManager<GameModels>(new List<GameModel>());
            Campaign.Current._gameModels = gameModels;
        }

        Campaign.Current.Models.MapVisibilityModel = new FixedMapVisibilityModel();
    }

    private static CampaignVec2 LandPosition(float x, float y) => new CampaignVec2(new Vec2(x, y), true);

    private sealed class FixedMapVisibilityModel : MapVisibilityModel
    {
        public override float MaximumSeeingRange() => 100f;

        public override float GetPartySeeingRangeBase(MobileParty party) => 50f;

        public override ExplainedNumber GetPartySpottingRange(MobileParty party, bool includeDescriptions = false) =>
            new ExplainedNumber(50f);

        public override float GetPartySpottingRatioForMainPartySeeingRange(MobileParty party) => 1f;

        public override float GetHideoutSpottingDistance() => 30f;
    }
}
