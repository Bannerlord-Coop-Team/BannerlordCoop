﻿using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CharacterAttributeSerializationTest
    {
        IContainer container;
        public CharacterAttributeSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void CharacterAttribute_Serialize()
        {
            CharacterAttribute testCharacterAttribute = new CharacterAttribute("test");

            var factory = container.Resolve<IBinaryPackageFactory>();
            CharacterAttributeBinaryPackage package = new CharacterAttributeBinaryPackage(testCharacterAttribute, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void CharacterAttribute_Full_Serialization()
        {
            CharacterAttribute characterAttribute = new CharacterAttribute("test");
            var objectManager = container.Resolve<IObjectManager>();

            objectManager.AddExisting(characterAttribute.StringId, characterAttribute);

            var factory = container.Resolve<IBinaryPackageFactory>();
            CharacterAttributeBinaryPackage package = new CharacterAttributeBinaryPackage(characterAttribute, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CharacterAttributeBinaryPackage>(obj);

            CharacterAttributeBinaryPackage returnedPackage = (CharacterAttributeBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            CharacterAttribute newCharacterAttribute = returnedPackage.Unpack<CharacterAttribute>(deserializeFactory);

            Assert.Same(characterAttribute, newCharacterAttribute);
        }
    }
}
