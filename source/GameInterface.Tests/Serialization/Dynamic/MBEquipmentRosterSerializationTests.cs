using GameInterface.Serialization.Dynamic;
using HarmonyLib;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Tests.Serialization.Dynamic
{
    public class MBEquipmentRosterSerializationTests
    {
        private readonly ITestOutputHelper output;
        public MBEquipmentRosterSerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void NominalMBEquipmentRosterObjectSerialization()
        {
            var testModel = MakeMBEquipmentRosterSerializable();

            MBEquipmentRoster mbEquipmentRoster = new MBEquipmentRoster();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);

            byte[] data = ser.Serialize(mbEquipmentRoster);

            MBEquipmentRoster newMBEquipmentRoster = ser.Deserialize<MBEquipmentRoster>(data);

            Assert.NotNull(newMBEquipmentRoster);
        }

        [Fact]
        public void NullMBEquipmentRosterObjectSerialization()
        {
            var testModel = MakeMBEquipmentRosterSerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);

            MBEquipmentRoster newMBEquipmentRoster = ser.Deserialize<MBEquipmentRoster>(data);

            Assert.Null(newMBEquipmentRoster);
        }

        private RuntimeTypeModel MakeMBEquipmentRosterSerializable()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<MBEquipmentRoster>();

            generator.AssignSurrogate<Equipment, SurrogateStub<Equipment>>();
            generator.AssignSurrogate<BasicCultureObject, SurrogateStub<BasicCultureObject>>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(MBEquipmentRoster)));

            return testModel;
        }
    }
}
