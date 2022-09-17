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
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Tests.Serialization.Dynamic
{
    public class ItemModifierSerializationTests
    {
        private readonly ITestOutputHelper output;
        public ItemModifierSerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void NominalItemModifierObjectSerialization()
        {
            var testModel = MakeItemModifierSerializable();

            ItemModifier itemModifier = new ItemModifier();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);

            byte[] data = ser.Serialize(itemModifier);

            ItemModifier newItemModifier = ser.Deserialize<ItemModifier>(data);

            Assert.NotNull(newItemModifier);
        }

        [Fact]
        public void NullItemModifierObjectSerialization()
        {
            var testModel = MakeItemModifierSerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);

            ItemModifier newItemModifier = ser.Deserialize<ItemModifier>(data);

            Assert.Null(newItemModifier);
        }

        private RuntimeTypeModel MakeItemModifierSerializable()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<ItemModifier>();

            generator.AssignSurrogate<TextObject, SurrogateStub<TextObject>>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(ItemModifier)));

            return testModel;
        }
    }
}
