using Common.Messaging;
using Common.Serialization;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Data;
using GameInterface.Surrogates;
using ProtoBuf;
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

        [Fact]
        public void NetworkChangeHeroName_RoundTrips_OnlyNullAttribute()
        {
            var name = new TextObject("{=!}{KINGDOM_NAME}", new Dictionary<string, object>
            {
                ["KINGDOM_NAME"] = null,
            });

            var copy = RoundTrip(new NetworkChangeHeroName(new HeroChangeNameData(CreateHero(), name, null)));

            Assert.NotNull(copy.Data.FullName.Attributes);
            Assert.Single(copy.Data.FullName.Attributes);
            Assert.True(copy.Data.FullName.Attributes.ContainsKey("KINGDOM_NAME"));
            Assert.Null(copy.Data.FullName.Attributes["KINGDOM_NAME"]);
            Assert.False(copy.Data.FullName.Attributes.ContainsKey("ABSENT"));
        }

        [Fact]
        public void NetworkChangeHeroName_RoundTrips_NullAttributesIncludingNestedValues()
        {
            var faction = new TextObject("{=!}{KINGDOM_NAME} {LINK}", new Dictionary<string, object>
            {
                ["KINGDOM_NAME"] = null,
                ["LINK"] = "Kingdom-Created_1",
                ["ZERO"] = 0f,
                ["FRACTION"] = -1.25f,
            });
            var name = new TextObject("{=!}{FACTION}", new Dictionary<string, object>
            {
                ["FACTION"] = faction,
                ["NULL"] = null,
                ["AGE"] = 42,
                ["ZERO"] = 0f,
                ["FRACTION"] = 2.5f,
            });

            var copy = RoundTrip(new NetworkChangeHeroName(new HeroChangeNameData(CreateHero(), name, null)));

            var attributes = copy.Data.FullName.Attributes;
            Assert.Equal(5, attributes.Count);
            Assert.True(attributes.ContainsKey("NULL"));
            Assert.Null(attributes["NULL"]);
            Assert.False(attributes.ContainsKey("ABSENT"));
            Assert.Equal(42, Assert.IsType<int>(attributes["AGE"]));
            Assert.Equal(0f, Assert.IsType<float>(attributes["ZERO"]));
            Assert.Equal(2.5f, Assert.IsType<float>(attributes["FRACTION"]));
            var nested = Assert.IsType<TextObject>(attributes["FACTION"]);
            Assert.Equal(faction.Value, nested.Value);
            Assert.Equal(4, nested.Attributes.Count);
            Assert.True(nested.Attributes.ContainsKey("KINGDOM_NAME"));
            Assert.Null(nested.Attributes["KINGDOM_NAME"]);
            Assert.False(nested.Attributes.ContainsKey("ABSENT"));
            Assert.Equal("Kingdom-Created_1", Assert.IsType<string>(nested.Attributes["LINK"]));
            Assert.Equal(0f, Assert.IsType<float>(nested.Attributes["ZERO"]));
            Assert.Equal(-1.25f, Assert.IsType<float>(nested.Attributes["FRACTION"]));
        }

        [Fact]
        public void TextObjectSurrogate_ReadsLegacyPayloadWithoutOptionalAttributes()
        {
            // Field 1 contains "legacy"; all optional attribute fields are absent.
            using var stream = new MemoryStream(new byte[] { 0x0a, 6, 108, 101, 103, 97, 99, 121 });

            var copy = Serializer.Deserialize<TextObject>(stream);

            Assert.Equal("legacy", copy.Value);
            Assert.Null(copy.Attributes);
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
