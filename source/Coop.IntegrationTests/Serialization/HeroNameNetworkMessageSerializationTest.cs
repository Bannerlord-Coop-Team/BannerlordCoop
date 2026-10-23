using Common.Messaging;
using Common.Serialization;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Data;
using GameInterface.Surrogates;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace Coop.IntegrationTests.Serialization
{
    public class HeroNameNetworkMessageSerializationTest
    {
        private readonly ProtoBufSerializer serializer;

        public HeroNameNetworkMessageSerializationTest()
        {
            // Registers TextObjectSurrogate
            _ = new SurrogateCollection();
            serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        }

        [Fact]
        public void NetworkChangeHeroName_RoundTrips_NullNames()
        {
            var copy = RoundTrip(new NetworkChangeHeroName(new HeroChangeNameData(CreateHero(), null, null)));

            Assert.Equal("lord_1_1", copy.Data.HeroStringId);
            Assert.Null(copy.Data.FullName);
            Assert.Null(copy.Data.FirstName);
        }

        [Fact]
        public void NetworkChangeHeroName_RoundTrips_NameAttributes()
        {
            var firstName = new TextObject("{=!}Lucon");
            var fullName = new TextObject("{=!}{FIRSTNAME} {CLAN_NAME} {AGE}", new Dictionary<string, object>
            {
                ["FIRSTNAME"] = new TextObject("{=!}Lucon"),
                ["CLAN_NAME"] = "dey Meroc",
                ["AGE"] = 42,
            });

            var copy = RoundTrip(new NetworkChangeHeroName(new HeroChangeNameData(CreateHero(), fullName, firstName)));

            Assert.Equal(firstName.Value, copy.Data.FirstName.Value);
            Assert.Equal(fullName.Value, copy.Data.FullName.Value);

            var attributes = copy.Data.FullName.Attributes;
            Assert.Equal(3, attributes.Count);
            Assert.Equal("{=!}Lucon", Assert.IsType<TextObject>(attributes["FIRSTNAME"]).Value);
            Assert.Equal("dey Meroc", attributes["CLAN_NAME"]);
            Assert.Equal(42, attributes["AGE"]);
        }

        [Fact]
        public void NetworkChangeHeroName_RoundTrips_FloatAttributesIncludingNestedValues()
        {
            var name = new TextObject("{=!}{REWARD_GOLD} {NESTED}");
            name.SetTextVariable("REWARD_GOLD", 2500f);
            name.SetTextVariable("NESTED", new TextObject("{=!}{ZERO} {FRACTION}")
                .SetTextVariable("ZERO", 0f).SetTextVariable("FRACTION", -1.25f));

            var copy = RoundTrip(new NetworkChangeHeroName(new HeroChangeNameData(CreateHero(), name, null)));

            Assert.Equal(2500f, Assert.IsType<float>(copy.Data.FullName.Attributes["REWARD_GOLD"]));
            var nested = Assert.IsType<TextObject>(copy.Data.FullName.Attributes["NESTED"]);
            Assert.Equal(0f, Assert.IsType<float>(nested.Attributes["ZERO"]));
            Assert.Equal(-1.25f, Assert.IsType<float>(nested.Attributes["FRACTION"]));
        }

        private static Hero CreateHero()
        {
            var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            hero.StringId = "lord_1_1";
            return hero;
        }

        private NetworkChangeHeroName RoundTrip(NetworkChangeHeroName message)
        {
            byte[] bytes = serializer.Serialize(message);
            var copy = Assert.IsType<NetworkChangeHeroName>(serializer.Deserialize<IMessage>(bytes));
            Assert.Equal(bytes, serializer.Serialize(copy));
            return copy;
        }
    }
}
