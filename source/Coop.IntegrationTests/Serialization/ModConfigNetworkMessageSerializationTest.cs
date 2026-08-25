using GameInterface.Configuration;
using GameInterface.Services.CampaignService.Messages;
using ProtoBuf;
using ProtoBuf.Meta;

namespace Coop.IntegrationTests.Serialization
{
    /// <summary>
    /// Locks the wire contract for <see cref="NetworkLoadModConfig"/> — the message that pushes the
    /// host's resolved options to every client.
    /// <para>
    /// The shape is unusual and the mistake it invites is invisible: <see cref="ModOptions"/> carries
    /// property initializers that are NOT the protobuf defaults, so an option whose documented default
    /// is <c>true</c> travels as an OMITTED field when the operator configures it <c>false</c>, and
    /// arrives correctly only because deserialization starts from a zeroed struct. Nothing about that
    /// is visible at a call site: get it wrong and clients silently keep their own defaults for every
    /// option the host turned off, with no error anywhere.
    /// </para>
    /// <para>
    /// Measured while writing these (protobuf-net 3.2.56): declaring a parameterless constructor on
    /// <see cref="ModOptions"/> does NOT break this — protobuf-net builds value types as
    /// <c>default</c> and never calls it, on the plain model and on a <c>UseConstructor</c> one alike.
    /// These tests assert the contract itself rather than that one hypothesis, so they still bite if
    /// the wire shape is changed some other way (member optionality, <c>SkipConstructor</c>, an added
    /// <c>[DefaultValue]</c>).
    /// </para>
    /// </summary>
    public class ModConfigNetworkMessageSerializationTest
    {
        /// <summary>Every option set to the value protobuf treats as absent — the direction that only
        /// survives because the receiver starts zeroed. <c>GoldFoodChangeMode.Disabled</c> is enum 0,
        /// so it is omitted too and must not arrive as the <c>OneDayMax</c> default.</summary>
        [Fact]
        public void NetworkLoadModConfig_RoundTrips_OptionsTurnedOff()
        {
            var options = AllOptionsOff();

            var copy = RoundTrip(new NetworkLoadModConfig(options)).ModOptions;

            AssertAllOptionsOff(copy);
        }

        /// <summary>The same, through a model told to build the struct via its constructor. Wine-Mono
        /// rejects protobuf-net's uninitialized-object factory for value types, so
        /// <c>ProtoBufSerializer.ConfigureMonoValueType</c> forces exactly this on Linux/Proton
        /// clients, on the stated assumption that the no-factory path still yields a zeroed value.
        /// This pins that assumption for the one message whose correctness depends on it.</summary>
        [Fact]
        public void NetworkLoadModConfig_RoundTrips_OptionsTurnedOff_WhenModelUsesTheConstructor()
        {
            var model = RuntimeTypeModel.Create();
            model.Add(typeof(ModOptions), applyDefaultBehaviour: true).UseConstructor = true;
            model.Add(typeof(NetworkLoadModConfig), applyDefaultBehaviour: true).UseConstructor = true;

            var copy = RoundTrip(new NetworkLoadModConfig(AllOptionsOff()), model).ModOptions;

            AssertAllOptionsOff(copy);
        }

        /// <summary>The ordinary direction: configured values that differ from both the documented
        /// defaults and protobuf's zero arrive intact.</summary>
        [Fact]
        public void NetworkLoadModConfig_RoundTrips_ConfiguredValues()
        {
            var options = new ModOptions(new ModOptionsData
            {
                ClientsCanUseCheats = true,
                GoldFoodInfluenceChangeInBattles = GoldFoodChangeMode.Enabled,
                GoldFoodInfluenceChangeForDisconnectedPlayers = true,
                PlayerBattleAiJoinWindowHours = 6,
                WandererLimit = 64,
                WandererLimitScalesWithPlayers = true,
                PlayerKingdomClanTierRequired = 2,
                SmithingStaminaRecoveryMultiplier = 2.5f,
                SmithingCraftedItemsValueMultiplier = 3.5f,
                MaximumLootersMultiplier = 0.25f,
                LooterPartySizeMultiplier = 0.33f,
                ShowPlayerNameplates = true,
                PlayerWoundedBattleEntry = false,
            });

            var copy = RoundTrip(new NetworkLoadModConfig(options)).ModOptions;

            Assert.True(copy.ClientsCanUseCheats);
            Assert.Equal(GoldFoodChangeMode.Enabled, copy.GoldFoodInfluenceChangeInBattles);
            Assert.True(copy.GoldFoodInfluenceChangeForDisconnectedPlayers);
            Assert.Equal(6, copy.PlayerBattleAiJoinWindowHours);
            Assert.Equal(64, copy.WandererLimit);
            Assert.True(copy.WandererLimitScalesWithPlayers);
            Assert.Equal(2, copy.PlayerKingdomClanTierRequired);
            Assert.Equal(2.5f, copy.SmithingStaminaRecoveryMultiplier);
            Assert.Equal(3.5f, copy.SmithingCraftedItemsValueMultiplier);
            Assert.Equal(0.25f, copy.MaximumLootersMultiplier);
            Assert.Equal(0.33f, copy.LooterPartySizeMultiplier);
            Assert.False(copy.PlayerWoundedBattleEntry);

            // Keys the operator left absent still resolve to the documented defaults, not to zero.
            Assert.True(copy.FastForwardEnabled);
            Assert.True(copy.AutoPauseEnabled);
            Assert.True(copy.SpeedLimitWhilePlayersInBattle);
            Assert.True(copy.ShowPlayerNameplates);
        }

        private static ModOptions AllOptionsOff() => new(new ModOptionsData
        {
            FastForwardEnabled = false,
            AutoPauseEnabled = false,
            ClientsCanUseCheats = false,
            GoldFoodInfluenceChangeInSettlements = false,
            GoldFoodInfluenceChangeInBattles = GoldFoodChangeMode.Disabled,
            GoldFoodInfluenceChangeForDisconnectedPlayers = false,
            PlayerBattleAiJoinWindowHours = 0,
            SpeedLimitWhilePlayersInBattle = false,
            WandererLimit = 0,
            WandererLimitScalesWithPlayers = false,
            PlayerKingdomClanTierRequired = 0,
            SmithingStaminaRecoveryOutsideSettlements = false,
            SmithingStaminaRecoveryMultiplier = 0f,
            SmithingCraftedItemsValueMultiplier = 0f,
            MaximumLootersMultiplier = 0f,
            LooterPartySizeMultiplier = 0f,
            ShowPlayerNameplates = false,
            PlayerWoundedBattleEntry = false,
        });

        private static void AssertAllOptionsOff(ModOptions copy)
        {
            Assert.False(copy.FastForwardEnabled);
            Assert.False(copy.AutoPauseEnabled);
            Assert.False(copy.ClientsCanUseCheats);
            Assert.False(copy.GoldFoodInfluenceChangeInSettlements);
            Assert.Equal(GoldFoodChangeMode.Disabled, copy.GoldFoodInfluenceChangeInBattles);
            Assert.False(copy.GoldFoodInfluenceChangeForDisconnectedPlayers);
            Assert.Equal(0, copy.PlayerBattleAiJoinWindowHours);
            Assert.False(copy.SpeedLimitWhilePlayersInBattle);
            Assert.Equal(0, copy.WandererLimit);
            Assert.False(copy.WandererLimitScalesWithPlayers);
            Assert.Equal(0, copy.PlayerKingdomClanTierRequired);
            Assert.False(copy.SmithingStaminaRecoveryOutsideSettlements);
            Assert.Equal(0f, copy.SmithingStaminaRecoveryMultiplier);
            Assert.Equal(0f, copy.SmithingCraftedItemsValueMultiplier);
            Assert.Equal(0f, copy.MaximumLootersMultiplier);
            Assert.Equal(0f, copy.LooterPartySizeMultiplier);
            Assert.False(copy.ShowPlayerNameplates);
            Assert.False(copy.PlayerWoundedBattleEntry);
        }

        private static T RoundTrip<T>(T original)
        {
            using var stream = new MemoryStream();
            Serializer.Serialize(stream, original);
            stream.Position = 0;
            return Serializer.Deserialize<T>(stream);
        }

        private static T RoundTrip<T>(T original, RuntimeTypeModel model)
        {
            using var stream = new MemoryStream();
            model.Serialize(stream, original);
            stream.Position = 0;
            return (T)model.Deserialize(stream, value: null, typeof(T));
        }
    }
}
