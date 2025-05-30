using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Alleys;
public class AlleySyncTests : SyncTestBase
{
    private string alleyId;

    public AlleySyncTests(ITestOutputHelper output) : base(output)
	{
        alleyId = TestEnvironment.CreateRegisteredObject<Alley>();
        TestEnvironment.CreateRegisteredObject<Hero>();
        TestEnvironment.CreateRegisteredObject<Settlement>();
    }

    [Fact]
    public void Server_Alley_Fields()
    {
        Server.ObjectManager.TryGetObject<Alley>(alleyId, out Alley serverInstance);
        Assert.NotNull(serverInstance.Settlement);
        TestEnvironment.AssertField<Alley, TextObject>(nameof(Alley._name), new TextObject("test alley name"), defaultValue: new TextObject("TestAlley"));
        TestEnvironment.AssertReferenceField<Alley, Settlement>(nameof(Alley._settlement), defaultValue: serverInstance.Settlement);
        TestEnvironment.AssertField<Alley, string>(nameof(Alley._tag), "testtag", defaultValue: "tag");
        TestEnvironment.AssertReferenceField<Alley, Hero>(nameof(Alley._owner));
    }
}
