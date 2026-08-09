using Common.Network.Session;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Session
{
    public class SessionListingDataCodecTests
    {
        private static string Read(IReadOnlyDictionary<string, string> data, string key)
        {
            return data.TryGetValue(key, out var value) ? value : string.Empty;
        }

        private static PlatformIdentity ProviderIdentity(ulong userId) =>
            new PlatformIdentity("provider", userId.ToString());

        [Fact]
        public void RoundTrip_PreservesJoinInfo()
        {
            var info = new SessionJoinInfo
            {
                Address = "203.0.113.7",
                Port = 4200,
                TunnelTarget = ProviderIdentity(76561198000000042),
                DedicatedServer = true,
                ModVersion = Common.ModInformation.BuildVersion,
                PasswordRequired = true,
                ConnectedPlayers = 3,
                Password = "must-not-be-advertised",
            };

            var encoded = SessionListingDataCodec.Encode(info);
            Assert.True(SessionListingDataCodec.TryDecode(key => Read(encoded, key), out var decoded, out var error));

            Assert.Null(error);
            Assert.Equal(SessionJoinInfo.CurrentVersion, decoded.Version);
            Assert.Equal("203.0.113.7", decoded.Address);
            Assert.Equal(4200, decoded.Port);
            Assert.Equal(ProviderIdentity(76561198000000042), decoded.TunnelTarget);
            Assert.Equal(Common.ModInformation.BuildVersion, decoded.ModVersion);
            Assert.True(decoded.PasswordRequired);
            Assert.Equal(3, decoded.ConnectedPlayers);
            Assert.Null(decoded.Password);
            Assert.True(decoded.HasAddress);
            Assert.True(decoded.HasTunnelTarget);
            Assert.DoesNotContain(encoded.Values, value => value.Contains("must-not-be-advertised"));
            Assert.Equal(SessionListingDataCodec.DedicatedListingType, encoded[SessionListingDataCodec.ListingTypeKey]);
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

            var encoded = SessionListingDataCodec.Encode(info);
            Assert.True(SessionListingDataCodec.TryDecode(key => Read(encoded, key), out var decoded, out _));

            Assert.False(decoded.HasAddress);
        }

        [Theory]
        [InlineData(ServerVisibility.Public, "public")]
        [InlineData(ServerVisibility.FriendsOnly, "friends_only")]
        [InlineData(ServerVisibility.None, "none")]
        public void Visibility_RoundTripsCanonicalValues(ServerVisibility visibility, string encoded)
        {
            Assert.Equal(encoded, SessionListingDataCodec.EncodeVisibility(visibility));
            Assert.True(SessionListingDataCodec.TryDecodeVisibility(encoded, out var decoded));
            Assert.Equal(visibility, decoded);
        }

        [Fact]
        public void Visibility_MissingMetadataDefaultsToPublicForOlderLobbies()
        {
            Assert.True(SessionListingDataCodec.TryDecodeVisibility(string.Empty, out var visibility));
            Assert.Equal(ServerVisibility.Public, visibility);
        }

        [Fact]
        public void Visibility_UnknownMetadataFailsClosed()
        {
            Assert.False(SessionListingDataCodec.TryDecodeVisibility("unexpected", out var visibility));
            Assert.Equal(ServerVisibility.None, visibility);
        }

        [Fact]
        public void Encode_UnlistedStandaloneUsesHiddenLobbyMarkerButStillDecodes()
        {
            var info = new SessionJoinInfo
            {
                Port = 4200,
                TunnelTarget = ProviderIdentity(76561198000000042),
                DedicatedServer = true,
                ModVersion = Common.ModInformation.BuildVersion,
                Discoverable = false,
            };

            var encoded = SessionListingDataCodec.Encode(info);

            Assert.Equal(SessionListingDataCodec.HiddenDedicatedListingType,
                encoded[SessionListingDataCodec.ListingTypeKey]);
            Assert.True(SessionListingDataCodec.TryDecode(key => Read(encoded, key), out var decoded, out _));
            Assert.False(decoded.Discoverable);
            Assert.True(decoded.HasTunnelTarget);
        }

        [Fact]
        public void Encode_PlayerListingTypeIsUnaffectedByDiscoverabilityFlag()
        {
            var encoded = SessionListingDataCodec.Encode(new SessionJoinInfo
            {
                Port = 4200,
                Discoverable = false,
            });

            Assert.Equal(SessionListingDataCodec.PlayerListingType, encoded[SessionListingDataCodec.ListingTypeKey]);
        }

        [Fact]
        public void Decode_FailsWithoutVersion()
        {
            Assert.False(SessionListingDataCodec.TryDecode(_ => string.Empty, out var info, out var error));

            Assert.Null(info);
            Assert.NotNull(error);
        }

        [Fact]
        public void Decode_RejectsOlderVersion()
        {
            var data = new Dictionary<string, string>
            {
                [SessionListingDataCodec.VersionKey] = "1",
                [SessionListingDataCodec.AddressKey] = "203.0.113.7",
                [SessionListingDataCodec.PortKey] = "4200",
                [SessionListingDataCodec.ModVersionKey] = Common.ModInformation.BuildVersion,
            };

            Assert.False(SessionListingDataCodec.TryDecode(key => Read(data, key), out _, out var error));
            Assert.Contains("older", error);
        }

        [Fact]
        public void Decode_AcceptsCanonicalPasswordRequiredFlag()
        {
            var data = SessionListingDataCodec.Encode(new SessionJoinInfo
            {
                Port = 4200,
                ModVersion = Common.ModInformation.BuildVersion,
            });
            var mutable = new Dictionary<string, string>(data)
            {
                [SessionListingDataCodec.PasswordRequiredKey] = "1",
            };

            Assert.True(SessionListingDataCodec.TryDecode(key => Read(mutable, key), out var decoded, out _));
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
            var data = SessionListingDataCodec.Encode(new SessionJoinInfo
            {
                Port = 4200,
                ModVersion = Common.ModInformation.BuildVersion,
            });
            var mutable = new Dictionary<string, string>(data)
            {
                [SessionListingDataCodec.PasswordRequiredKey] = value,
            };

            Assert.True(SessionListingDataCodec.TryDecode(key => Read(mutable, key), out var decoded, out _));
            Assert.False(decoded.PasswordRequired);
        }

        [Theory]
        [InlineData("", 0)]
        [InlineData("not-a-count", 0)]
        [InlineData("-1", 0)]
        [InlineData("3", 3)]
        public void Decode_UsesSafeConnectedPlayerCount(string value, int expected)
        {
            var data = new Dictionary<string, string>(SessionListingDataCodec.Encode(new SessionJoinInfo
            {
                Port = 4200,
                ModVersion = Common.ModInformation.BuildVersion,
            }))
            {
                [SessionListingDataCodec.ConnectedPlayersKey] = value,
            };

            Assert.True(SessionListingDataCodec.TryDecode(key => Read(data, key), out var decoded, out _));
            Assert.Equal(expected, decoded.ConnectedPlayers);
        }

        [Fact]
        public void Encode_ClampsNegativeConnectedPlayerCount()
        {
            var encoded = SessionListingDataCodec.Encode(new SessionJoinInfo
            {
                Port = 4200,
                ConnectedPlayers = -1,
            });

            Assert.Equal("0", encoded[SessionListingDataCodec.ConnectedPlayersKey]);
        }

        [Fact]
        public void Decode_RejectsDifferentModVersion()
        {
            var data = SessionListingDataCodec.Encode(new SessionJoinInfo
            {
                Port = 4200,
                ModVersion = Common.ModInformation.BuildVersion + ".different",
            });

            Assert.False(SessionListingDataCodec.TryDecode(key => Read(data, key), out _, out var error));

            Assert.Contains("mod", error);
            Assert.Contains(Common.ModInformation.BuildVersion, error);
        }

        [Fact]
        public void Decode_RejectsMissingModVersion()
        {
            var data = new Dictionary<string, string>(SessionListingDataCodec.Encode(new SessionJoinInfo
            {
                Port = 4200,
                ModVersion = Common.ModInformation.BuildVersion,
            }));
            data.Remove(SessionListingDataCodec.ModVersionKey);

            Assert.False(SessionListingDataCodec.TryDecode(key => Read(data, key), out _, out var error));

            Assert.Contains("did not advertise", error);
        }

        [Fact]
        public void Decode_FailsOnNewerVersion()
        {
            var data = new Dictionary<string, string>
            {
                [SessionListingDataCodec.VersionKey] = (SessionJoinInfo.CurrentVersion + 1).ToString(),
                [SessionListingDataCodec.AddressKey] = "203.0.113.7",
                [SessionListingDataCodec.PortKey] = "4200",
            };

            Assert.False(SessionListingDataCodec.TryDecode(key => Read(data, key), out _, out var error));

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
                [SessionListingDataCodec.VersionKey] = SessionJoinInfo.CurrentVersion.ToString(),
                [SessionListingDataCodec.PortKey] = port,
                [SessionListingDataCodec.ModVersionKey] = Common.ModInformation.BuildVersion,
            };

            Assert.False(SessionListingDataCodec.TryDecode(key => Read(data, key), out _, out var error));

            Assert.NotNull(error);
        }
    }
}
