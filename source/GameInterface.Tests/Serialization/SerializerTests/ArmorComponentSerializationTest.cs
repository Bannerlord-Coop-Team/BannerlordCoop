using System;
using System.Linq;
using System.Reflection;
using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ArmorComponentSerializationTest
    {
        public ArmorComponentSerializationTest()
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void ArmorComponent_Serialize()
        {
            ItemObject itemObject = MBObjectManager.Instance.CreateObject<ItemObject>();
            ArmorComponent ArmorComponent = new ArmorComponent(itemObject);

            foreach(var property in typeof(ArmorComponent).GetProperties())
            {
                property.SetRandom(ArmorComponent);
            }

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ArmorComponentBinaryPackage package = new ArmorComponentBinaryPackage(ArmorComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void ArmorComponent_Full_Serialization()
        {
            ItemObject itemObject = MBObjectManager.Instance.CreateObject<ItemObject>();
            ArmorComponent ArmorComponent = new ArmorComponent(itemObject);

            foreach (var property in typeof(ArmorComponent).GetProperties())
            {
                property.SetRandom(ArmorComponent);
            }

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ArmorComponentBinaryPackage package = new ArmorComponentBinaryPackage(ArmorComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ArmorComponentBinaryPackage>(obj);

            ArmorComponentBinaryPackage returnedPackage = (ArmorComponentBinaryPackage)obj;

            ArmorComponent newArmorComponent = returnedPackage.Unpack<ArmorComponent>();

            CompareRecursive(ArmorComponent, newArmorComponent, typeof(ArmorComponent));
        }

        private void CompareRecursive(object? expected, object? actual, Type type)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            foreach (var property in type.GetProperties().Where(p => p.GetIndexParameters().Length == 0))
            {
                if (property.PropertyType.IsPrimitive || property.PropertyType == typeof(string))
                {
                    object? expectedValue;
                    object? actualValue;
                    try
                    {
                        expectedValue = property.GetValue(expected);
                        actualValue = property.GetValue(actual);
                    }
                    catch (TargetInvocationException ex) when (ex.InnerException is NullReferenceException)
                    {
                        continue;
                    }
                    Assert.Equal(expectedValue, actualValue);
                }
                else
                {
                    CompareRecursive(property.GetValue(expected), property.GetValue(actual), property.PropertyType);
                }
            }
        }
    }
}
