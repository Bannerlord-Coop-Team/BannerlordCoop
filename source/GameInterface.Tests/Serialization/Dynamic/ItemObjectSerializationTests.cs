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
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Localization;
using TaleWorlds.Library;

namespace GameInterface.Tests.Serialization.Dynamic
{
    public class ItemObjectSerializationTests : IDisposable
    {
        private readonly ITestOutputHelper output;
        public ItemObjectSerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        public void Dispose()
        {
        }

        [Fact]
        public void NominalItemObjectObjectSerialization()
        {
            var testModel = MakeItemObjectSerializable();

            ItemObject itemObject = new ItemObject();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);

            byte[] data = ser.Serialize(itemObject);

            ItemObject newItemObject = ser.Deserialize<ItemObject>(data);

            Assert.NotNull(newItemObject);
        }

        [Fact]
        public void NullItemObjectObjectSerialization()
        {
            var testModel = MakeItemObjectSerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);

            ItemObject newItemObject = ser.Deserialize<ItemObject>(data);

            Assert.Null(newItemObject);
        }

        private RuntimeTypeModel MakeItemObjectSerializable()
        {
            string[] excluded = new string[]
            {

            };

            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<ItemObject>(excluded);

            generator.AssignSurrogate<ItemComponent, SurrogateStub<ItemComponent>>();
            generator.AssignSurrogate<TextObject, SurrogateStub<TextObject>>();
            generator.AssignSurrogate<ItemCategory, SurrogateStub<ItemCategory>>();
            generator.AssignSurrogate<BasicCultureObject, SurrogateStub<BasicCultureObject>>();
            generator.AssignSurrogate<WeaponDesign, SurrogateStub<WeaponDesign  >>();
            generator.AssignSurrogate<BasicCultureObject, SurrogateStub<BasicCultureObject>>();
            generator.AssignSurrogate<Vec3, SurrogateStub<Vec3>>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(ItemObject)));

            return testModel;
        }
    }
}
