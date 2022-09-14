using GameInterface.Serialization.Dynamic;
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
    public class EquipmentElementSerializationTests
    {
        private readonly ITestOutputHelper output;
        public EquipmentElementSerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void NominalEquipmentElementObjectSerialization()
        {
            var testModel = MakeEquipmentElementSerializable();

            EquipmentElement equipmentElement = new EquipmentElement(new ItemObject("Test"));

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);

            byte[] data = ser.Serialize(equipmentElement);

            EquipmentElement newEquipmentElement = ser.Deserialize<EquipmentElement>(data);

            Assert.NotEqual(default, newEquipmentElement);
        }

        [Fact]
        public void NullEquipmentElementObjectSerialization()
        {
            var testModel = MakeEquipmentElementSerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);

            EquipmentElement newEquipmentElement = ser.Deserialize<EquipmentElement>(data);

            Assert.Equal(default(EquipmentElement).GetHashCode(), newEquipmentElement.GetHashCode());
        }

        private RuntimeTypeModel MakeEquipmentElementSerializable()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<EquipmentElement>();

            generator.AssignSurrogate<ItemObject, SurrogateStub<ItemObject>>();
            generator.AssignSurrogate<ItemModifier, SurrogateStub<ItemModifier>>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(EquipmentElement)));

            return testModel;
        }
    }
}
