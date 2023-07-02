using System;
using System.Linq;
using System.Reflection;
using Autofac;
using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using TaleWorlds.Core;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ArmorComponentSerializationTest
    {
        IContainer container;
        public ArmorComponentSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void ArmorComponent_Serialize()
        {
            ItemObject itemObject = new ItemObject("Attached Item");
            ArmorComponent ArmorComponent = new ArmorComponent(itemObject);

            foreach(var property in typeof(ArmorComponent).GetProperties())
            {
                property.SetRandom(ArmorComponent);
            }

            var factory = container.Resolve<IBinaryPackageFactory>();
            ArmorComponentBinaryPackage package = new ArmorComponentBinaryPackage(ArmorComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void ArmorComponent_Full_Serialization()
        {
            var objectManager = container.Resolve<IObjectManager>();
            ItemObject itemObject = new ItemObject("Attached Item");
            ArmorComponent ArmorComponent = new ArmorComponent(itemObject);

            // Register object with new string id
            Assert.True(objectManager.AddExisting(itemObject.StringId, itemObject));

            foreach (var property in typeof(ArmorComponent).GetProperties())
            {
                property.SetRandom(ArmorComponent);
            }

            var factory = container.Resolve<IBinaryPackageFactory>();
            ArmorComponentBinaryPackage package = new ArmorComponentBinaryPackage(ArmorComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ArmorComponentBinaryPackage>(obj);

            ArmorComponentBinaryPackage returnedPackage = (ArmorComponentBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            ArmorComponent newArmorComponent = returnedPackage.Unpack<ArmorComponent>(deserializeFactory);

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
