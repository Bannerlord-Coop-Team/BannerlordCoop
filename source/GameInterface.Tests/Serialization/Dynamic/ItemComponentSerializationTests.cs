﻿using GameInterface.Serialization.Dynamic;
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
using static TaleWorlds.Core.HorseComponent;

namespace GameInterface.Tests.Serialization.Dynamic
{
    public class ItemComponentSerializationTests
    {
        private readonly ITestOutputHelper output;
        public ItemComponentSerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        public static IEnumerable<object[]> ItemComponents => new List<object[]>
        {
            new[] { new HorseComponent() },
            new[] { new SaddleComponent(null) },
            new[] { new ArmorComponent(new ItemObject()) },
            new[] { new TradeItemComponent() },
            new[] { new WeaponComponent(new ItemObject()) },
        };

        [Theory]
        [MemberData(nameof(ItemComponents))]
        public void NominalItemComponentObjectSerialization(object component)
        {
            var testModel = MakeItemComponentSerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);

            byte[] data = ser.Serialize(component);

            ItemComponent newItemComponent = ser.Deserialize<ItemComponent>(data);

            Assert.NotNull(newItemComponent);
        }

        [Fact]
        public void NullItemComponentObjectSerialization()
        {
            var testModel = MakeItemComponentSerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);

            ItemComponent newItemComponent = ser.Deserialize<ItemComponent>(data);

            Assert.Null(newItemComponent);
        }

        private RuntimeTypeModel MakeItemComponentSerializable()
        {
            string[] excluded = new string[]
            {

            };

            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<ArmorComponent>(excluded);
            generator.CreateDynamicSerializer<HorseComponent>(excluded);
            generator.CreateDynamicSerializer<SaddleComponent>(excluded);
            generator.CreateDynamicSerializer<TradeItemComponent>(excluded);
            generator.CreateDynamicSerializer<WeaponComponent>(excluded);
            generator.CreateDynamicSerializer<WeaponComponentData>();
            generator.CreateDynamicSerializer<MaterialProperty>();

            generator.CreateDynamicSerializer<ItemComponent>()
                .AddDerivedType<ArmorComponent>()
                .AddDerivedType<HorseComponent>()
                .AddDerivedType<SaddleComponent>()
                .AddDerivedType<TradeItemComponent>()
                .AddDerivedType<WeaponComponent>();

            generator.AssignSurrogate<ItemObject, SurrogateStub<ItemObject>>();
            generator.AssignSurrogate<TextObject, SurrogateStub<TextObject>>();
            generator.AssignSurrogate<Monster, SurrogateStub<Monster>>();
            generator.AssignSurrogate<WeaponComponentData, SurrogateStub<WeaponComponentData>>();
            generator.AssignSurrogate<ItemModifierGroup, SurrogateStub<ItemModifierGroup>>();
            generator.AssignSurrogate<SkeletonScale, SurrogateStub<SkeletonScale>>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(ItemComponent)));

            return testModel;
        }
    }
}
