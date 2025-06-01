using E2E.Tests.Util;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BasicCultureObjects
{
    public class BasicCultureObjectSyncTests : SyncTestBase
    {
        public BasicCultureObjectSyncTests(ITestOutputHelper output) : base(output)
        {
            TestEnvironment.CreateRegisteredObject<BasicCultureObject>();
        }

        [Fact]
        public void Server_BasicCultureObject_Properties()
        {
            // Arrange
            TestEnvironment.AssertProperty<BasicCultureObject, uint>(nameof(BasicCultureObject.BackgroundColor1), 11U);
            TestEnvironment.AssertProperty<BasicCultureObject, uint>(nameof(BasicCultureObject.BackgroundColor2), 22U);
            TestEnvironment.AssertProperty<BasicCultureObject, string>(nameof(BasicCultureObject.BannerKey), "testBanner");
            TestEnvironment.AssertProperty<BasicCultureObject, bool>(nameof(BasicCultureObject.CanHaveSettlement), true);
            TestEnvironment.AssertProperty<BasicCultureObject, uint>(nameof(BasicCultureObject.ClothAlternativeColor),33U);
            TestEnvironment.AssertProperty<BasicCultureObject, uint>(nameof(BasicCultureObject.ClothAlternativeColor2),44U);
            TestEnvironment.AssertProperty<BasicCultureObject, uint>(nameof(BasicCultureObject.Color), 55U);
            TestEnvironment.AssertProperty<BasicCultureObject, uint>(nameof(BasicCultureObject.Color2), 66U);
            TestEnvironment.AssertProperty<BasicCultureObject, string>(nameof(BasicCultureObject.EncounterBackgroundMesh), "testMesh");
            TestEnvironment.AssertProperty<BasicCultureObject, uint>(nameof(BasicCultureObject.ForegroundColor1),77U);
            TestEnvironment.AssertProperty<BasicCultureObject, uint>(nameof(BasicCultureObject.ForegroundColor2), 88U);
            TestEnvironment.AssertProperty<BasicCultureObject, bool>(nameof(BasicCultureObject.IsBandit), true);
            TestEnvironment.AssertProperty<BasicCultureObject, bool>(nameof(BasicCultureObject.IsMainCulture), true);
            TestEnvironment.AssertProperty<BasicCultureObject, TextObject>(nameof(BasicCultureObject.Name), new TextObject("testName"));
        }
    }
}
