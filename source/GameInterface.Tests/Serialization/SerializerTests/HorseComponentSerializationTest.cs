using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit;
using Common.Serialization;
using GameInterface.Services.ObjectManager;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class HorseComponentSerializationTest
    {
        IContainer container;
        public HorseComponentSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void HorseComponent_Serialize()
        {
            HorseComponent HorseComponent = new HorseComponent();
            HorseComponent.Monster = new Monster();

            var factory = container.Resolve<IBinaryPackageFactory>();
            HorseComponentBinaryPackage package = new HorseComponentBinaryPackage(HorseComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo _monsterMaterialNames = typeof(HorseComponent).GetField("_monsterMaterialNames", BindingFlags.NonPublic | BindingFlags.Instance);
        [Fact]
        public void HorseComponent_Full_Serialization()
        {
            HorseComponent HorseComponent = new HorseComponent();

            HorseComponent.BodyLength = 5;
            HorseComponent.ChargeDamage = 6;
            HorseComponent.HitPointBonus = 8;
            HorseComponent.IsPackAnimal = false;
            HorseComponent.IsRideable = false;
            HorseComponent.Maneuver = 55;
            HorseComponent.Speed = 513;

            Monster monster = MBObjectManager.Instance.CreateObject<Monster>();
            monster.StringId = "testMonster";

            HorseComponent.Monster = monster;

            var objectManager = container.Resolve<IObjectManager>();
            objectManager.AddExisting(monster.StringId, monster);

            _monsterMaterialNames.SetValue(HorseComponent, new MBList<HorseComponent.MaterialProperty>
            {
                new HorseComponent.MaterialProperty("mat1"),
                new HorseComponent.MaterialProperty("mat2"),
            });


            var factory = container.Resolve<IBinaryPackageFactory>();
            HorseComponentBinaryPackage package = new HorseComponentBinaryPackage(HorseComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<HorseComponentBinaryPackage>(obj);

            HorseComponentBinaryPackage returnedPackage = (HorseComponentBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            HorseComponent newHorseComponent = returnedPackage.Unpack<HorseComponent>(deserializeFactory);

            Assert.Equal(HorseComponent.BodyLength, newHorseComponent.BodyLength);
            Assert.Equal(HorseComponent.ChargeDamage, newHorseComponent.ChargeDamage);
            Assert.Equal(HorseComponent.HideCount, newHorseComponent.HideCount);
            Assert.Equal(HorseComponent.HitPointBonus, newHorseComponent.HitPointBonus);
            Assert.Equal(HorseComponent.HitPoints, newHorseComponent.HitPoints);
            Assert.Equal(HorseComponent.HorseMaterialNames.Count, newHorseComponent.HorseMaterialNames.Count);
            Assert.Equal(HorseComponent.IsLiveStock, newHorseComponent.IsLiveStock);
            Assert.Equal(HorseComponent.IsMount, newHorseComponent.IsMount);
            Assert.Equal(HorseComponent.IsPackAnimal, newHorseComponent.IsPackAnimal);
            Assert.Equal(HorseComponent.IsRideable, newHorseComponent.IsRideable);
            Assert.Equal(HorseComponent.Maneuver, newHorseComponent.Maneuver);
            Assert.Equal(HorseComponent.MeatCount, newHorseComponent.MeatCount);
            Assert.Equal(HorseComponent.Speed, newHorseComponent.Speed);
        }
    }
}
