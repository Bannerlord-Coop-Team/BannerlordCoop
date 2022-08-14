using Autofac;
using Coop.Serialization;
using GameInterface.Serialization.DynamicModel;
using GameInterface.Serialization.Models;
using ProtoBuf.Meta;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit;
using Xunit.Abstractions;

namespace GameInterface.Tests
{
    public class DynamicModelGeneratorTests
    {
        private readonly ITestOutputHelper output;

        public DynamicModelGeneratorTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void AddTypeDynamicallyToProtobuf()
        {
            string[] excluded = new string[]
            {
                "<ItemCategory>k__BackingField",
                "<Culture>k__BackingField",
                "<WeaponDesign>k__BackingField",
            };

            IDynamicModelGenerator generator = new DynamicModelGenerator();

            generator.CreateDynamicSerializer<ItemObject>(excluded);
            generator.CreateDynamicSerializer<ItemComponent>();
            generator.CreateDynamicSerializer<Vec3>();

            generator.AssignSurrogate<TextObject, TextObjectSurrogate>();

            generator.Compile();

            ItemObject itemObject = new ItemObject();

            ProtobufSerializer ser = new ProtobufSerializer();
            byte[] data = ser.Serialize(itemObject);
            ItemObject newItemObject = ser.Deserialize<ItemObject>(data);

            Assert.True(RuntimeTypeModel.Default.CanSerialize(typeof(ItemObject)));
        }
    }
}