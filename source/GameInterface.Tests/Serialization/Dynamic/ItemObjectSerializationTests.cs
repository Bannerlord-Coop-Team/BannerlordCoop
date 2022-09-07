using GameInterface.Serialization.Dynamic;
using ProtoBuf.Meta;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit;
using Xunit.Abstractions;

namespace GameInterface.Tests.Serialization.Dynamic
{
    public class ItemObjectSerializationTests
    {
        private readonly ITestOutputHelper output;

        public ItemObjectSerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private RuntimeTypeModel MakeItemObjectSerializable()
        {
            string[] excluded = new string[]
            {
                "<ItemCategory>k__BackingField",
                "<Culture>k__BackingField",
                "<WeaponDesign>k__BackingField",
            };

            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<ItemObject>(excluded);

            generator.AssignSurrogate<TextObject, SurrogateStub<TextObject>>();
            generator.AssignSurrogate<ItemComponent, SurrogateStub<ItemComponent>>();
            generator.AssignSurrogate<Vec3, SurrogateStub<Vec3>>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(ItemObject)));

            return testModel;
        }

        [Fact]
        public void NominalItemObjectSerialization()
        {
            var testModel = MakeItemObjectSerializable();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(ItemObject)));

            ItemObject itemObject = new ItemObject();
            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(itemObject);
            ItemObject newItem = ser.Deserialize<ItemObject>(data);

            Assert.NotNull(newItem);
        }

        [Fact]
        public void NullItemObjectSerialization()
        {
            var testModel = MakeItemObjectSerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);
            ItemObject newItem = ser.Deserialize<ItemObject>(data);

            Assert.Null(newItem);
        }
    }
}
