using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;


namespace E2E.Tests.Services.PartyComponents;
public class MilitiaPartyComponentSyncTests : SyncTestBase
{
    private string militiaPartyComponentId;

    public MilitiaPartyComponentSyncTests(ITestOutputHelper output) : base(output)
    {
        militiaPartyComponentId = TestEnvironment.CreateRegisteredObject<MilitiaPartyComponent>();
        TestEnvironment.CreateRegisteredObject<Settlement>();
    }

    [Fact]
    public void Server_MilitiaPartyComponent_Properties()
    {
        TestEnvironment.Server.ObjectManager.TryGetObject(militiaPartyComponentId, out MilitiaPartyComponent militiaPartyComponent);
        militiaPartyComponent.Settlement = null;
        TestEnvironment.AssertReferenceProperty<MilitiaPartyComponent, Settlement>(nameof(MilitiaPartyComponent.Settlement));
    }
}