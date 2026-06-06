using E2E.Tests.Util;
using Xunit.Abstractions;
using TaleWorlds.CampaignSystem.Party;

namespace E2E.Tests.Services.MobilePartyAis
{
    public class MobilePartyAiTest : SyncTestBase
    {
        private string _aiId;
        private string _secondPartyId;

        public MobilePartyAiTest(ITestOutputHelper output) : base(output)
        {
            _aiId = TestEnvironment.CreateRegisteredObject<MobilePartyAi>();
            _secondPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        }


        [Fact]
        public void Server_MobilePartyAi_Fields()
        {
            // Required as mobileParty Ai comes with a predefined mobile party
            var mobilePartyAi = TestEnvironment.Server.GetRegisteredObject<MobilePartyAi>(_aiId);
            //Disabled: _mobileParty sync is disabled in MobilePartyAiSync.cs (readonly, done in lifetime handler)
            //TestEnvironment.AssertReferenceField<MobilePartyAi, MobileParty>(nameof(MobilePartyAi._mobileParty), referenceStringId: _secondPartyId, defaultValue: mobilePartyAi._mobileParty);
            TestEnvironment.AssertField<MobilePartyAi, bool>(nameof(MobilePartyAi._isDisabled), true);
        }

        [Fact]
        public void Server_MobilePartyAi_Properties()
        {
            //TestEnvironment.AssertProperty<MobilePartyAi, AiBehavior>(nameof(MobilePartyAi.DefaultBehavior), AiBehavior.BesiegeSettlement);
            //TestEnvironment.AssertProperty<MobilePartyAi, MoveModeType>(nameof(MobilePartyAi.PartyMoveMode), MoveModeType.Escort);
            //TestEnvironment.AssertReferenceProperty<MobilePartyAi, MobileParty>(nameof(MobilePartyAi.MoveTargetParty));
        }
    }
}
