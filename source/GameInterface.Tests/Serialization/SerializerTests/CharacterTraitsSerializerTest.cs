using GameInterface.Serialization.External;
using GameInterface.Serialization;
using TaleWorlds.Core;
using Xunit;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using System.Collections.Generic;
using TaleWorlds.ObjectSystem;
using GameInterface.Tests.Bootstrap;
using Autofac;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Services.ObjectManager;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CharacterTraitsSerializationTest
    {
        IContainer container;
        public CharacterTraitsSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void CharacterTraits_Serialize()
        {
            CharacterTraits CharacterTraits = new CharacterTraits();

            var factory = container.Resolve<IBinaryPackageFactory>();
            CharacterTraitsBinaryPackage package = new CharacterTraitsBinaryPackage(CharacterTraits, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo _attributes = typeof(PropertyOwner<TraitObject>).GetField("_attributes", BindingFlags.Instance | BindingFlags.NonPublic);
        [Fact]
        public void CharacterTraits_Full_Serialization()
        {
            CharacterTraits characterTraits = new CharacterTraits();
            var objectManager = container.Resolve<IObjectManager>();

            TraitObject trait1 = new TraitObject("Trait1");
            TraitObject trait2 = new TraitObject("Trait2");

            objectManager.AddExisting(trait1.StringId, trait1);
            objectManager.AddExisting(trait2.StringId, trait2);

            Dictionary<TraitObject, int> traits = new Dictionary<TraitObject, int>
            {
                { trait1, 3 },
                { trait2, 7 },
            };
            _attributes.SetValue(characterTraits, traits);

            var factory = container.Resolve<IBinaryPackageFactory>();
            CharacterTraitsBinaryPackage package = new CharacterTraitsBinaryPackage(characterTraits, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CharacterTraitsBinaryPackage>(obj);

            CharacterTraitsBinaryPackage returnedPackage = (CharacterTraitsBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            CharacterTraits newCharacterTraits = returnedPackage.Unpack<CharacterTraits>(deserializeFactory);

            Assert.Equal(characterTraits.Id, newCharacterTraits.Id);
            Assert.Equal(characterTraits.StringId, newCharacterTraits.StringId);
            Assert.Equal(characterTraits.IsReady, newCharacterTraits.IsReady);

            Dictionary<TraitObject, int> newTraits = (Dictionary<TraitObject, int>)_attributes.GetValue(newCharacterTraits);

            Assert.Equal(traits.ToString(), newTraits.ToString());
        }
    }
}
