using Coop.Serialization;
using GameInterface.Serialization;
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
    public class SerializationTests
    {
        private readonly ITestOutputHelper output;

        public SerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void AddTypeDynamicallyToProtobuf()
        {
            Type[] excluded = new Type[]
            {
                typeof(ItemCategory),
                typeof(BasicCultureObject),
                typeof(WeaponDesign),
            };

            DynamicModelGenerator.CreateDynamicSerializer<ItemObject>(excluded);
            DynamicModelGenerator.CreateDynamicSerializer<ItemComponent>();
            DynamicModelGenerator.CreateDynamicSerializer<Vec3>();

            DynamicModelGenerator.AssignSurrogate<TextObject, TextObjectSurrogate>();

            DynamicModelGenerator.Compile();

            ItemObject itemObject = new ItemObject();

            ProtobufSerializer ser = new ProtobufSerializer();
            byte[] data = ser.Serialize(itemObject);
            ItemObject newItemObject = ser.Deserialize<ItemObject>(data);

            Assert.True(RuntimeTypeModel.Default.CanSerialize(typeof(ItemObject)));
        }
    }
}