using Common.Network.Session;
using Coop.Steam;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Steam
{
    public class LobbyDataCodecTests
    {
        private static string Read(IReadOnlyDictionary<string, string> data, string key)
        {
            return data.TryGetValue(key, out var value) ? value : string.Empty;
        }

        [Fact]
        public void RoundTrip_PreservesJoinInfo()
        {
            var info = new SessionJoinInfo
            {
                Address = "203.0.113.7",
                Port = 4200,
                ServerSteamId = 76561198000000042,
                ModVersion = Common.ModInformation.BuildVersion,
                PasswordRequired = true,
                Password = "must-not-be-advertised",
            };

            var encoded = LobbyDataCodec.Encode(info);
            Assert.True(LobbyDataCodec.TryDecode(key => Read(encoded, key), out var decoded, out var error));

            Assert.Null(error);
            Assert.Equal(SessionJoinInfo.CurrentVersion, decoded.Version);
            Assert.Equal("203.0.113.7", decoded.Address);
            Assert.Equal(4200, decoded.Port);
            Assert.Equal(76561198000000042UL, decoded.ServerSteamId);
            Assert.Equal(Common.ModInformation.BuildVersion, decoded.ModVersion);
            Assert.True(decoded.PasswordRequired);
            Assert.Null(decoded.Password);
            Assert.True(decoded.HasAddress);
            Assert.True(decoded.HasServerSteamId);
            Assert.DoesNotContain(encoded.Values, value => value.Contains("must-not-be-advertised"));
            Assert.Equal(LobbyDataCodec.StandaloneLobbyType, encoded[LobbyDataCodec.LobbyTypeKey]);
        }

        [Fact]
        public void RoundTrip_PreservesEmptyAddress()
        {
            var info = new SessionJoinInfo
            {
                Address = null,
                Port = 4200,
                ModVersion = Common.ModInformation.BuildVersion,
            };

            var encoded = LobbyDataCodec.Encode(info);
            Assert.True(LobbyDataCodec.TryDecode(key => Read(encoded, key), out var decoded, out _));

            Assert.False(decoded.HasAddress);
        }

        [Theory]
        [InlineData(ServerVisibility.Public, "public")]
        [InlineData(ServerVisibility.FriendsOnly, "friends_only")]
        [InlineData(ServerVisibility.None, "none")]
        public void Visibility_RoundTripsCanonicalValues(ServerVisibility visibility, string encoded)
        {
            Assert.Equal(encoded, LobbyDataCodec.EncodeVisibility(visibility));
            Assert.True(LobbyDataCodec.TryDecodeVisibility(encoded, out var decoded));
            Assert.Equal(visibility, decoded);
        }

        [Fact]
        public void Visibility_MissingMetadataDefaultsToPublicForOlderLobbies()
        {
            Assert.True(LobbyDataCodec.TryDecodeVisibility(string.Empty, out var visibility));
            Assert.Equal(ServerVisibility.Public, visibility);
        }

        [Fact]
        public void Visibility_UnknownMetadataFailsClosed()
        {
            Assert.False(LobbyDataCodec.TryDecodeVisibility("unexpected", out var visibility));
            Assert.Equal(ServerVisibility.None, visibility);
        }

        [Fact]
        public void Encode_UnlistedStandaloneUsesHiddenLobbyMarkerButStillDecodes()
        {
            var info = new SessionJoinInfo
            {
                Port = 4200,
                ServerSteamId = 76561198000000042,
                ModVersion = Common.ModInformation.BuildVersion,
                Discoverable = false,
            };

            var encoded = LobbyDataCodec.Encode(info);

            Assert.Equal(LobbyDataCodec.HiddenStandaloneLobbyType,
                encoded[LobbyDataCodec.LobbyTypeKey]);
            Assert.True(LobbyDataCodec.TryDecode(key => Read(encoded, key), out var decoded, out _));
            Assert.False(decoded.Discoverable);
            Assert.True(decoded.HasServerSteamId);
        }

        [Fact]
        public void Encode_PlayerLobbyTypeIsUnaffectedByDiscoverabilityFlag()
        {
            var encoded = LobbyDataCodec.Encode(new SessionJoinInfo
            {
                Port = 4200,
                Discoverable = false,
            });

            Assert.Equal(LobbyDataCodec.PlayerLobbyType, encoded[LobbyDataCodec.LobbyTypeKey]);
        }

        [Fact]
        public void Decode_FailsWithoutVersion()
        {
            Assert.False(LobbyDataCodec.TryDecode(_ => string.Empty, out var info, out var error));

            Assert.Null(info);
            Assert.NotNull(error);
        }

        [Fact]
        public void Decode_AcceptsOlderVersion()
        {
            var data = new Dictionary<string, string>
            {
                [LobbyDataCodec.VersionKey] = "1",
                [LobbyDataCodec.AddressKey] = "203.0.113.7",
                [LobbyDataCodec.PortKey] = "4200",
                [LobbyDataCodec.ModVersionKey] = Common.ModInformation.BuildVersion,
            };

            Assert.True(LobbyDataCodec.TryDecode(key => Read(data, key), out var decoded, out _));

            Assert.Equal(1, decoded.Version);
            Assert.True(decoded.Version < SessionJoinInfo.MinTunnelVersion);
            Assert.False(decoded.HasServerSteamId);
            Assert.Equal(Common.ModInformation.BuildVersion, decoded.ModVersion);
            Assert.False(decoded.PasswordRequired);
        }

        [Fact]
        public void Decode_AcceptsCanonicalPasswordRequiredFlag()
        {
            var data = LobbyDataCodec.Encode(new SessionJoinInfo
            {
                Port = 4200,
                ModVersion = Common.ModInformation.BuildVersion,
            });
            var mutable = new Dictionary<string, string>(data)
            {
                [LobbyDataCodec.PasswordRequiredKey] = "1",
            };

            Assert.True(LobbyDataCodec.TryDecode(key => Read(mutable, key), out var decoded, out _));
            Assert.True(decoded.PasswordRequired);
        }

        [Theory]
        [InlineData("")]
        [InlineData("0")]
        [InlineData("true")]
        [InlineData("True")]
        [InlineData("not-a-flag")]
        public void Decode_TreatsNonCanonicalPasswordFlagAsFalse(string value)
        {
            var data = LobbyDataCodec.Encode(new SessionJoinInfo
            {
                Port = 4200,
                ModVersion = Common.ModInformation.BuildVersion,
            });
            var mutable = new Dictionary<string, string>(data)
            {
                [LobbyDataCodec.PasswordRequiredKey] = value,
            };

            Assert.True(LobbyDataCodec.TryDecode(key => Read(mutable, key), out var decoded, out _));
            Assert.False(decoded.PasswordRequired);
        }

        [Fact]
        public void Decode_RejectsDifferentModVersion()
        {
            var data = LobbyDataCodec.Encode(new SessionJoinInfo
            {
                Port = 4200,
                ModVersion = Common.ModInformation.BuildVersion + ".different",
            });

            Assert.False(LobbyDataCodec.TryDecode(key => Read(data, key), out _, out var error));

            Assert.Contains("mod", error);
            Assert.Contains(Common.ModInformation.BuildVersion, error);
        }

        [Fact]
        public void Decode_RejectsMissingModVersion()
        {
            var data = new Dictionary<string, string>(LobbyDataCodec.Encode(new SessionJoinInfo
            {
                Port = 4200,
                ModVersion = Common.ModInformation.BuildVersion,
            }));
            data.Remove(LobbyDataCodec.ModVersionKey);

            Assert.False(LobbyDataCodec.TryDecode(key => Read(data, key), out _, out var error));

            Assert.Contains("did not advertise", error);
        }

        [Fact]
        public void Decode_FailsOnNewerVersion()
        {
            var data = new Dictionary<string, string>
            {
                [LobbyDataCodec.VersionKey] = (SessionJoinInfo.CurrentVersion + 1).ToString(),
                [LobbyDataCodec.AddressKey] = "203.0.113.7",
                [LobbyDataCodec.PortKey] = "4200",
            };

            Assert.False(LobbyDataCodec.TryDecode(key => Read(data, key), out _, out var error));

            Assert.Contains("newer", error);
        }

        [Theory]
        [InlineData("not-a-port")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("70000")]
        public void Decode_FailsOnInvalidPort(string port)
        {
            var data = new Dictionary<string, string>
            {
                [LobbyDataCodec.VersionKey] = SessionJoinInfo.CurrentVersion.ToString(),
                [LobbyDataCodec.PortKey] = port,
                [LobbyDataCodec.ModVersionKey] = Common.ModInformation.BuildVersion,
            };

            Assert.False(LobbyDataCodec.TryDecode(key => Read(data, key), out _, out var error));

            Assert.NotNull(error);
        }
    }
}
