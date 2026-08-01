using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Modules;
using ProtoBuf;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Server.Connections.Messages
{
    public class NetworkModuleVersionsValidateTests
    {
        [Fact]
        public void Serialize_RoundTripsBuildVersion()
        {
            var original = new NetworkModuleVersionsValidate(
                new List<ModuleInfo>
                {
                    new ModuleInfo("Coop", false, false, new ApplicationVersion()),
                },
                "0.1.1+0123456789012345678901234567890123456789");

            using var stream = new MemoryStream();
            Serializer.Serialize(stream, original);
            stream.Position = 0;

            var deserialized = Serializer.Deserialize<NetworkModuleVersionsValidate>(stream);

            Assert.Equal(original.BuildVersion, deserialized.BuildVersion);
            Assert.Single(deserialized.Modules);
        }
    }
}
