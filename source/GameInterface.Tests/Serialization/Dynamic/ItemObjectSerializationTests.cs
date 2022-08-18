using Coop.Serialization;
using GameInterface.Serialization.Dynamic;
using GameInterface.Serialization.Surrogates;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        [Fact]
        public void NominalItemObjectDynamicSerialization()
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
            generator.CreateDynamicSerializer<ItemComponent>();
            generator.CreateDynamicSerializer<Vec3>();

            generator.AssignSurrogate<TextObject, TextObjectSurrogate>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(ItemObject)));

            ItemObject itemObject = new ItemObject();
            typeof(ItemObject).GetProperty("Name").SetValue(itemObject, new TextObject("name"));

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(itemObject);
            ItemObject newItemObject = ser.Deserialize<ItemObject>(data);

            Assert.Equal(itemObject.Name.ToString(), newItemObject.Name.ToString());
        }
    }
}
