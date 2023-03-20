using Common.Extensions;
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
    public class MobilePartyAISerializationTest
    {
        public MobilePartyAISerializationTest()
        {
            GameBootStrap.Initialize();
        }
        
        [Fact]
        public void MobilePartyAi_Serialize()
        {
            MobilePartyAi PartyAI = (MobilePartyAi)FormatterServices.GetUninitializedObject(typeof(MobilePartyAi));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            MobilePartyAIBinaryPackage package = new MobilePartyAIBinaryPackage(PartyAI, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo _isDisabled = typeof(MobilePartyAi).GetField("_isDisabled", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _mobileParty = typeof(MobilePartyAi).GetField("_mobileParty", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BehaviorTarget = typeof(MobilePartyAi).GetField("BehaviorTarget", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _attackInitiative = typeof(MobilePartyAi).GetField("_attackInitiative", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _avoidInitiative = typeof(MobilePartyAi).GetField("_avoidInitiative", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _initiativeRestoreTime = typeof(MobilePartyAi).GetField("_initiativeRestoreTime", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _aiBehaviorResetNeeded = typeof(MobilePartyAi).GetField("_aiBehaviorResetNeeded", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _nextAiCheckTime = typeof(MobilePartyAi).GetField("_nextAiCheckTime", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo DefaultBehaviorNeedsUpdate = typeof(MobilePartyAi).GetField("DefaultBehaviorNeedsUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _numberOfRecentFleeingFromAParty = typeof(MobilePartyAi).GetField("_numberOfRecentFleeingFromAParty", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _defaultBehavior = typeof(MobilePartyAi).GetField("_defaultBehavior", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _aiPathMode = typeof(MobilePartyAi).GetField("_aiPathMode", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _aiPathNeeded = typeof(MobilePartyAi).GetField("_aiPathNeeded", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _formationPosition = typeof(MobilePartyAi).GetField("_formationPosition", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _moveTargetPoint = typeof(MobilePartyAi).GetField("_moveTargetPoint", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _aiPathLastPosition = typeof(MobilePartyAi).GetField("_aiPathLastPosition", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _aiPathNotFound = typeof(MobilePartyAi).GetField("_aiPathNotFound", BindingFlags.NonPublic | BindingFlags.Instance);
        [Fact]
        public void MobilePartyAi_Full_Serialization()
        {
            MobileParty party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));

            party.StringId = "myParty";

            MBObjectManager.Instance.RegisterObject(party);

            // Class setup
            MobilePartyAi PartyAI = (MobilePartyAi)FormatterServices.GetUninitializedObject(typeof(MobilePartyAi));

            _mobileParty.SetValue(PartyAI, party);
            _isDisabled.SetRandom(PartyAI);
            BehaviorTarget.SetValue(PartyAI, new Vec2(2, 3));
            _attackInitiative.SetRandom(PartyAI);
            _avoidInitiative.SetRandom(PartyAI);
            _initiativeRestoreTime.SetValue(PartyAI, new CampaignTime());
            _aiBehaviorResetNeeded.SetRandom(PartyAI);
            _nextAiCheckTime.SetValue(PartyAI, new CampaignTime());
            DefaultBehaviorNeedsUpdate.SetRandom(PartyAI);
            _numberOfRecentFleeingFromAParty.SetRandom(PartyAI);
            _defaultBehavior.SetRandom(PartyAI);
            _aiPathMode.SetRandom(PartyAI);
            _aiPathNeeded.SetRandom(PartyAI);
            _formationPosition.SetValue(PartyAI, new Vec2(2, 3));
            _moveTargetPoint.SetValue(PartyAI, new Vec2(2, 3));
            _aiPathLastPosition.SetValue(PartyAI, new Vec2(2, 3));


            PartyAI.HourCounter = 5;
            PartyAI.RethinkAtNextHourlyTick = true;


            BinaryPackageFactory factory = new BinaryPackageFactory();
            MobilePartyAIBinaryPackage package = new MobilePartyAIBinaryPackage(PartyAI, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<MobilePartyAIBinaryPackage>(obj);

            MobilePartyAIBinaryPackage returnedPackage = (MobilePartyAIBinaryPackage)obj;

            MobilePartyAi newPartyAI = returnedPackage.Unpack<MobilePartyAi>();

            
            Assert.Equal(_isDisabled.GetValue(PartyAI), _isDisabled.GetValue(newPartyAI));
            Assert.Equal(BehaviorTarget.GetValue(PartyAI), BehaviorTarget.GetValue(newPartyAI));
            Assert.Equal(_attackInitiative.GetValue(PartyAI), _attackInitiative.GetValue(newPartyAI));
            Assert.Equal(_avoidInitiative.GetValue(PartyAI), _avoidInitiative.GetValue(newPartyAI));
            Assert.Equal(_initiativeRestoreTime.GetValue(PartyAI), _initiativeRestoreTime.GetValue(newPartyAI));
            Assert.Equal(_aiBehaviorResetNeeded.GetValue(PartyAI), _aiBehaviorResetNeeded.GetValue(newPartyAI));
            Assert.Equal(_nextAiCheckTime.GetValue(PartyAI), _nextAiCheckTime.GetValue(newPartyAI));
            Assert.Equal(DefaultBehaviorNeedsUpdate.GetValue(PartyAI), DefaultBehaviorNeedsUpdate.GetValue(newPartyAI));
            Assert.Equal(_numberOfRecentFleeingFromAParty.GetValue(PartyAI), _numberOfRecentFleeingFromAParty.GetValue(newPartyAI));
            Assert.Equal(_defaultBehavior.GetValue(PartyAI), _defaultBehavior.GetValue(newPartyAI));
            Assert.Equal(_aiPathMode.GetValue(PartyAI), _aiPathMode.GetValue(newPartyAI));
            Assert.Equal(_aiPathNeeded.GetValue(PartyAI), _aiPathNeeded.GetValue(newPartyAI));
            Assert.Equal(_formationPosition.GetValue(PartyAI), _formationPosition.GetValue(newPartyAI));
            Assert.Equal(_moveTargetPoint.GetValue(PartyAI), _moveTargetPoint.GetValue(newPartyAI));
            Assert.Equal(_aiPathLastPosition.GetValue(PartyAI), _aiPathLastPosition.GetValue(newPartyAI));

            Assert.Equal(_mobileParty.GetValue(PartyAI), _mobileParty.GetValue(newPartyAI));
        }
    }
}
