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
using TaleWorlds.Localization;

namespace GameInterface.Tests.Serialization.Dynamic
{
    public class ItemModifierGroupSerializationTests : IDisposable
    {
        private readonly ITestOutputHelper output;
        public ItemModifierGroupSerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        public void Dispose()
        {
        }

        [Fact]
        public void NominalItemModifierGroupObjectSerialization()
        {
            var testModel = MakeItemModifierGroupSerializable();

            ItemModifierGroup itemModifierGroup = new ItemModifierGroup();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);

            byte[] data = ser.Serialize(itemModifierGroup);

            ItemModifierGroup newItemModifierGroup = ser.Deserialize<ItemModifierGroup>(data);

            Assert.NotNull(newItemModifierGroup);
        }

        [Fact]
        public void NullItemModifierGroupObjectSerialization()
        {
            var testModel = MakeItemModifierGroupSerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);

            ItemModifierGroup newItemModifierGroup = ser.Deserialize<ItemModifierGroup>(data);

            Assert.Null(newItemModifierGroup);
        }

        private RuntimeTypeModel MakeItemModifierGroupSerializable()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<ItemModifierGroup>();
            generator.CreateDynamicSerializer<ItemModifier>();
            generator.CreateDynamicSerializer<ItemModifierProbability>();

            generator.AssignSurrogate<TextObject, SurrogateStub<TextObject>>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(ItemModifierGroup)));

            return testModel;
        }
    }
}
