using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BasicCultureObjects
{
    public class BasicCultureObjectSyncTests : SyncTestBase
    {
        readonly string CultureObjectId;
        public BasicCultureObjectSyncTests(ITestOutputHelper output) : base(output)
        {
            CultureObjectId = TestEnvironment.CreateRegisteredObject<CultureObject>();
        }

        [Fact]
        public void Server_BasicCultureObject_Properties()
        {
            var assertHelper = TestEnvironment.CreateAssertHelper<CultureObject>(CultureObjectId);

            // Arrange
            assertHelper.AssertProperty<BasicCultureObject, uint>(nameof(BasicCultureObject.BackgroundColor1), 11U);
            assertHelper.AssertProperty<BasicCultureObject, uint>(nameof(BasicCultureObject.BackgroundColor2), 22U);
            //assertHelper.AssertProperty<BasicCultureObject, Banner>(nameof(BasicCultureObject.Banner), new Banner());
            assertHelper.AssertProperty<BasicCultureObject, bool>(nameof(BasicCultureObject.CanHaveSettlement), true);
            assertHelper.AssertProperty<BasicCultureObject, uint>(nameof(BasicCultureObject.ClothAlternativeColor),33U);
            assertHelper.AssertProperty<BasicCultureObject, uint>(nameof(BasicCultureObject.ClothAlternativeColor2),44U);
            assertHelper.AssertProperty<BasicCultureObject, uint>(nameof(BasicCultureObject.Color), 55U);
            assertHelper.AssertProperty<BasicCultureObject, uint>(nameof(BasicCultureObject.Color2), 66U);
            assertHelper.AssertProperty<BasicCultureObject, string>(nameof(BasicCultureObject.EncounterBackgroundMesh), "testMesh");
            assertHelper.AssertProperty<BasicCultureObject, uint>(nameof(BasicCultureObject.ForegroundColor1),77U);
            assertHelper.AssertProperty<BasicCultureObject, uint>(nameof(BasicCultureObject.ForegroundColor2), 88U);
            assertHelper.AssertProperty<BasicCultureObject, bool>(nameof(BasicCultureObject.IsBandit), true);
            assertHelper.AssertProperty<BasicCultureObject, bool>(nameof(BasicCultureObject.IsMainCulture), true);
            assertHelper.AssertProperty<BasicCultureObject, TextObject>(nameof(BasicCultureObject.Name), new TextObject("testName"));
        }
    }
}
