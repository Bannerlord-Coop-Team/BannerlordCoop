using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CharacterPerksSerializationTest
    {
        IContainer container;
        public CharacterPerksSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void CharacterPerks_Serialize()
        {
            CharacterPerks CharacterPerks = new CharacterPerks();

            var factory = container.Resolve<IBinaryPackageFactory>();
            CharacterPerksBinaryPackage package = new CharacterPerksBinaryPackage(CharacterPerks, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo _attributes = typeof(PropertyOwner<PerkObject>).GetField("_attributes", BindingFlags.NonPublic | BindingFlags.Instance);
        [Fact]
        public void CharacterPerks_Full_Serialization()
        {
            CharacterPerks characterPerks = new CharacterPerks();
            var objectManager = container.Resolve<IObjectManager>();

            characterPerks.StringId = "myCharacterPerks";

            objectManager.AddExisting(characterPerks.StringId, characterPerks);

            PerkObject perk1 = new PerkObject("MyPerk");
            PerkObject perk2 = new PerkObject("MyPerk2");
            PerkObject perk3 = new PerkObject("MyPerk3");

            objectManager.AddExisting(perk1.StringId, perk1);
            objectManager.AddExisting(perk2.StringId, perk2);
            objectManager.AddExisting(perk3.StringId, perk3);

            Dictionary<PerkObject, int> perks = new Dictionary<PerkObject, int>
            {
                { perk1, 5 },
                { perk2, 6 },
                { perk3, 7 }
            };

            _attributes.SetValue(characterPerks, perks);

            var factory = container.Resolve<IBinaryPackageFactory>();
            CharacterPerksBinaryPackage package = new CharacterPerksBinaryPackage(characterPerks, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CharacterPerksBinaryPackage>(obj);

            CharacterPerksBinaryPackage returnedPackage = (CharacterPerksBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            CharacterPerks newCharacterPerks = returnedPackage.Unpack<CharacterPerks>(deserializeFactory);

            Assert.Equal(characterPerks.StringId, characterPerks.StringId);
            Assert.Equal(characterPerks.Id, characterPerks.Id);
            Assert.Equal(characterPerks.IsReady, newCharacterPerks.IsReady);

            Dictionary<PerkObject, int> newPerks = (Dictionary<PerkObject, int>)_attributes.GetValue(newCharacterPerks);

            Assert.Equal(perks.ToString(), newPerks.ToString());
        }
    }
}
