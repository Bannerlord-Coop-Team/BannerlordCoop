using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class PartyAISerializationTest
    {
        public PartyAISerializationTest()
        {
            GameBootStrap.Initialize();
        }
        
        [Fact]
        public void PartyAI_Serialize()
        {
            MobilePartyAi PartyAI = (MobilePartyAi)FormatterServices.GetUninitializedObject(typeof(MobilePartyAi));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            PartyAIBinaryPackage package = new PartyAIBinaryPackage(PartyAI, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo _mobileParty = typeof(MobilePartyAi).GetField("_mobileParty", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _isDisabled = typeof(MobilePartyAi).GetField("_isDisabled", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo AiState = typeof(MobilePartyAi).GetProperty(nameof(MobilePartyAi.DefaultBehavior));
        private static readonly PropertyInfo DefaultBehavior = typeof(MobilePartyAi).GetProperty("DefaultBehavior", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo DefaultPosition = typeof(MobilePartyAi).GetProperty("DefaultPosition", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo DoNotMakeNewDecisions = typeof(MobilePartyAi).GetProperty(nameof(MobilePartyAi.DoNotMakeNewDecisions));
        private static readonly PropertyInfo Initiative = typeof(MobilePartyAi).GetProperty("Initiative", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo _enableAgainAtHour = typeof(MobilePartyAi).GetProperty("_enableAgainAtHour", BindingFlags.NonPublic | BindingFlags.Instance);
        [Fact]
        public void PartyAI_Full_Serialization()
        {
            MobileParty party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));

            party.StringId = "myParty";

            MBObjectManager.Instance.RegisterObject(party);

            // Class setup
            MobilePartyAi PartyAI = (MobilePartyAi)FormatterServices.GetUninitializedObject(typeof(MobilePartyAi));

            _mobileParty.SetValue(PartyAI, party);
            _isDisabled.SetValue(PartyAI, true);
            AiState.SetValue(PartyAI, AiBehavior.PatrolAroundPoint);
            DefaultBehavior.SetValue(PartyAI, AiBehavior.RaidSettlement);
            DefaultPosition.SetValue(PartyAI, new Vec2(2, 3));
            DoNotMakeNewDecisions.SetValue(PartyAI, true);
            Initiative.SetValue(PartyAI, 0.99f);
            _enableAgainAtHour.SetValue(PartyAI, new CampaignTime());

            PartyAI.HourCounter = 5;
            PartyAI.RethinkAtNextHourlyTick = true;


            BinaryPackageFactory factory = new BinaryPackageFactory();
            PartyAIBinaryPackage package = new PartyAIBinaryPackage(PartyAI, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<PartyAIBinaryPackage>(obj);

            PartyAIBinaryPackage returnedPackage = (PartyAIBinaryPackage)obj;

            MobilePartyAi newPartyAI = returnedPackage.Unpack<MobilePartyAi>();

            
            Assert.Equal(_isDisabled.GetValue(PartyAI), _isDisabled.GetValue(newPartyAI));
            Assert.Equal(AiState.GetValue(PartyAI), AiState.GetValue(newPartyAI));
            Assert.Equal(DefaultBehavior.GetValue(PartyAI), DefaultBehavior.GetValue(newPartyAI));
            Assert.Equal(DefaultPosition.GetValue(PartyAI), DefaultPosition.GetValue(newPartyAI));
            Assert.Equal(DoNotMakeNewDecisions.GetValue(PartyAI), DoNotMakeNewDecisions.GetValue(newPartyAI));
            Assert.Equal(Initiative.GetValue(PartyAI), Initiative.GetValue(newPartyAI));
            Assert.Equal(_enableAgainAtHour.GetValue(PartyAI), _enableAgainAtHour.GetValue(newPartyAI));

            Assert.Equal(_mobileParty.GetValue(PartyAI), _mobileParty.GetValue(newPartyAI));
        }
    }
}
