using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class HorseComponentSerializationTest
    {
        public HorseComponentSerializationTest()
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void HorseComponent_Serialize()
        {
            HorseComponent HorseComponent = new HorseComponent();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            HorseComponentBinaryPackage package = new HorseComponentBinaryPackage(HorseComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly PropertyInfo BodyLength = typeof(HorseComponent).GetProperty(nameof(HorseComponent.BodyLength));
        private static readonly PropertyInfo ChargeDamage = typeof(HorseComponent).GetProperty(nameof(HorseComponent.ChargeDamage));
        private static readonly PropertyInfo HitPointBonus = typeof(HorseComponent).GetProperty(nameof(HorseComponent.HitPointBonus));
        private static readonly PropertyInfo IsPackAnimal = typeof(HorseComponent).GetProperty(nameof(HorseComponent.IsPackAnimal));
        private static readonly PropertyInfo IsRideable = typeof(HorseComponent).GetProperty(nameof(HorseComponent.IsRideable));
        private static readonly PropertyInfo Maneuver = typeof(HorseComponent).GetProperty(nameof(HorseComponent.Maneuver));
        private static readonly PropertyInfo Speed = typeof(HorseComponent).GetProperty(nameof(HorseComponent.Speed));
        private static readonly PropertyInfo Monster = typeof(HorseComponent).GetProperty(nameof(HorseComponent.Monster));

        private static readonly FieldInfo _monsterMaterialNames = typeof(HorseComponent).GetField("_monsterMaterialNames", BindingFlags.NonPublic | BindingFlags.Instance);
        [Fact]
        public void HorseComponent_Full_Serialization()
        {
            HorseComponent HorseComponent = new HorseComponent();

            BodyLength.SetValue(HorseComponent, 5);
            ChargeDamage.SetValue(HorseComponent, 6);
            HitPointBonus.SetValue(HorseComponent, 8);
            IsPackAnimal.SetValue(HorseComponent, false);
            IsRideable.SetValue(HorseComponent, false);
            Maneuver.SetValue(HorseComponent, 55);
            Speed.SetValue(HorseComponent, 513);

            Monster monster = MBObjectManager.Instance.CreateObject<Monster>();
            Monster.SetValue(HorseComponent, monster);

            _monsterMaterialNames.SetValue(HorseComponent, new MBList<HorseComponent.MaterialProperty>
            {
                new HorseComponent.MaterialProperty("mat1"),
                new HorseComponent.MaterialProperty("mat2"),
            });


            BinaryPackageFactory factory = new BinaryPackageFactory();
            HorseComponentBinaryPackage package = new HorseComponentBinaryPackage(HorseComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<HorseComponentBinaryPackage>(obj);

            HorseComponentBinaryPackage returnedPackage = (HorseComponentBinaryPackage)obj;

            HorseComponent newHorseComponent = returnedPackage.Unpack<HorseComponent>();

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
