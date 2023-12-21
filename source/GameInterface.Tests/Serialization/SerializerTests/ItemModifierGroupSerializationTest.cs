﻿using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;
using Common.Serialization;
using System;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ItemModifierGroupSerializationTest
    {
        IContainer container;
        public ItemModifierGroupSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void ItemModifierGroup_Serialize()
        {
            ItemModifierGroup ItemModifierGroup = new ItemModifierGroup();

            var factory = container.Resolve<IBinaryPackageFactory>();
            ItemModifierGroupBinaryPackage package = new ItemModifierGroupBinaryPackage(ItemModifierGroup, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo _itemModifiers = typeof(ItemModifierGroup).GetField("_itemModifiers", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly FieldInfo _lootDropItemModifierScores = typeof(ItemModifierGroup).GetField("_lootDropItemModifierScores", BindingFlags.NonPublic | BindingFlags.Instance)!;
        [Fact]
        public void ItemModifierGroup_Full_Serialization()
        {
            ItemModifierGroup ItemModifierGroup = new ItemModifierGroup();

            var _modifiers = new MBList<ItemModifier>
            {
                new ItemModifier { StringId = "im1" },
                new ItemModifier { StringId = "im2"},
            };

            _itemModifiers.SetValue(ItemModifierGroup, _modifiers);

            var _lootDropItemModifiers = new MBList<ValueTuple<ItemModifier, float>>
            {
                new ValueTuple<ItemModifier, float>(null, 0f),
            };
            _lootDropItemModifierScores.SetValue(ItemModifierGroup, _lootDropItemModifiers);

            var factory = container.Resolve<IBinaryPackageFactory>();
            ItemModifierGroupBinaryPackage package = new ItemModifierGroupBinaryPackage(ItemModifierGroup, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ItemModifierGroupBinaryPackage>(obj);

            ItemModifierGroupBinaryPackage returnedPackage = (ItemModifierGroupBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            ItemModifierGroup newItemModifierGroup = returnedPackage.Unpack<ItemModifierGroup>(deserializeFactory);

            Assert.Equal(ItemModifierGroup.NoModifierLootScore, newItemModifierGroup.NoModifierLootScore);
            Assert.Equal(ItemModifierGroup.NoModifierProductionScore, newItemModifierGroup.NoModifierProductionScore);
            Assert.Equal(_modifiers.Count, newItemModifierGroup.ItemModifiers.Count);

            for (int i = 0; i < _modifiers.Count; i++)
            {
                Assert.Equal(ItemModifierGroup.ItemModifiers[i].ToString(), newItemModifierGroup.ItemModifiers[i].ToString());
            }
        }
    }
}
