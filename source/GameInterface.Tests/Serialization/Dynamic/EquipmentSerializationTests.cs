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

namespace GameInterface.Tests.Serialization.Dynamic
{
    public class EquipmentSerializationTests : IDisposable
    {
        private readonly ITestOutputHelper output;
        public EquipmentSerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        public void Dispose()
        {
        }

        [Fact]
        public void NominalEquipmentObjectSerialization()
        {
            var testModel = MakeEquipmentSerializable();

            Equipment equipment = new Equipment();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);

            byte[] data = ser.Serialize(equipment);

            Equipment newEquipment = ser.Deserialize<Equipment>(data);

            Assert.NotNull(newEquipment);
        }

        [Fact]
        public void NullEquipmentObjectSerialization()
        {
            var testModel = MakeEquipmentSerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);

            Equipment newEquipment = ser.Deserialize<Equipment>(data);

            Assert.Null(newEquipment);
        }

        private RuntimeTypeModel MakeEquipmentSerializable()
        {
            string[] excluded = new string[]
            {
                "SyncEquipments",
            };

            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<Equipment>(excluded);

            generator.AssignSurrogate<EquipmentElement, SurrogateStub<EquipmentElement>>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(Equipment)));

            return testModel;
        }
    }
}
