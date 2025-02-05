using Autofac;
using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class MobilePartyAISerializationTest
    {
        IContainer container;
        public MobilePartyAISerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }
        
        [Fact]
        public void MobilePartyAi_Serialize()
        {
            MobilePartyAi PartyAI = (MobilePartyAi)FormatterServices.GetUninitializedObject(typeof(MobilePartyAi));

            var factory = container.Resolve<IBinaryPackageFactory>();
            MobilePartyAIBinaryPackage package = new MobilePartyAIBinaryPackage(PartyAI, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo _mobileParty = typeof(MobilePartyAi).GetField("_mobileParty", BindingFlags.NonPublic | BindingFlags.Instance);
        [Fact]
        public void MobilePartyAi_Full_Serialization()
        {
            var objectManager = container.Resolve<IObjectManager>();
            MobileParty party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));

            party.StringId = "myParty";

            objectManager.AddExisting(party.StringId, party);

            // Class setup
            MobilePartyAi PartyAI = (MobilePartyAi)FormatterServices.GetUninitializedObject(typeof(MobilePartyAi));

            _mobileParty.SetValue(PartyAI, party);
            PartyAI._isDisabled = ReflectionExtensions.Random<bool>();
            PartyAI.BehaviorTarget = new Vec2(2, 3);
            PartyAI._attackInitiative = ReflectionExtensions.Random<float>();
            PartyAI._avoidInitiative = ReflectionExtensions.Random<float>();
            PartyAI._initiativeRestoreTime = new CampaignTime();
            PartyAI._aiBehaviorResetNeeded = ReflectionExtensions.Random<bool>();
            PartyAI._nextAiCheckTime = new CampaignTime();
            PartyAI.DefaultBehaviorNeedsUpdate = ReflectionExtensions.Random<bool>();
            PartyAI._numberOfRecentFleeingFromAParty = ReflectionExtensions.Random<int>();
            PartyAI._defaultBehavior = ReflectionExtensions.Random<AiBehavior>();
            PartyAI._aiPathMode = ReflectionExtensions.Random<bool>();
            PartyAI._aiPathNeeded = ReflectionExtensions.Random<bool>();
            PartyAI._formationPosition = new Vec2(2, 3);
            PartyAI._moveTargetPoint = new Vec2(2, 3);
            PartyAI._aiPathLastPosition = new Vec2(2, 3);


            PartyAI.HourCounter = 5;
            PartyAI.RethinkAtNextHourlyTick = true;


            var factory = container.Resolve<IBinaryPackageFactory>();
            MobilePartyAIBinaryPackage package = new MobilePartyAIBinaryPackage(PartyAI, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<MobilePartyAIBinaryPackage>(obj);

            MobilePartyAIBinaryPackage returnedPackage = (MobilePartyAIBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            MobilePartyAi newPartyAI = returnedPackage.Unpack<MobilePartyAi>(deserializeFactory);
            
            Assert.Equal(PartyAI._isDisabled, newPartyAI._isDisabled);
            Assert.Equal(PartyAI.BehaviorTarget, newPartyAI.BehaviorTarget);
            Assert.Equal(PartyAI._attackInitiative, newPartyAI._attackInitiative);
            Assert.Equal(PartyAI._avoidInitiative, newPartyAI._avoidInitiative);
            Assert.Equal(PartyAI._initiativeRestoreTime, newPartyAI._initiativeRestoreTime);
            Assert.Equal(PartyAI._aiBehaviorResetNeeded, newPartyAI._aiBehaviorResetNeeded);
            Assert.Equal(PartyAI._nextAiCheckTime, newPartyAI._nextAiCheckTime);
            Assert.Equal(PartyAI.DefaultBehaviorNeedsUpdate, newPartyAI.DefaultBehaviorNeedsUpdate);
            Assert.Equal(PartyAI._numberOfRecentFleeingFromAParty, newPartyAI._numberOfRecentFleeingFromAParty);
            Assert.Equal(PartyAI._defaultBehavior, newPartyAI._defaultBehavior);
            Assert.Equal(PartyAI._aiPathMode, newPartyAI._aiPathMode);
            Assert.Equal(PartyAI._aiPathNeeded, newPartyAI._aiPathNeeded);
            Assert.Equal(PartyAI._formationPosition, newPartyAI._formationPosition);
            Assert.Equal(PartyAI._moveTargetPoint, newPartyAI._moveTargetPoint);
            Assert.Equal(PartyAI._aiPathLastPosition, newPartyAI._aiPathLastPosition);
            Assert.Equal(PartyAI._mobileParty, newPartyAI._mobileParty);
        }
    }
}
