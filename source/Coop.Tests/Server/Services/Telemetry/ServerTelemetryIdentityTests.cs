using Coop.Core.Server.Services.Telemetry;
using System;
using System.IO;
using Xunit;

namespace Coop.Tests.Server.Services.Telemetry;

/// <summary>Checks stable server identity persistence used by public presence telemetry.</summary>
public class ServerTelemetryIdentityTests
{
    [Fact]
    public void LoadOrCreate_PersistsOneIdentityPerServerPort()
    {
        string directory = Path.Combine(Path.GetTempPath(), "coop-telemetry-" + Guid.NewGuid().ToString("N"));
        try
        {
            string first = ServerTelemetryIdentity.LoadOrCreate(
                directory,
                4200,
                () => "41fa0000000000000000000000000000");
            string second = ServerTelemetryIdentity.LoadOrCreate(
                directory,
                4200,
                () => "52fb0000000000000000000000000000");
            string otherPort = ServerTelemetryIdentity.LoadOrCreate(
                directory,
                4201,
                () => "63fc0000000000000000000000000000");

            Assert.Equal("41fa0000000000000000000000000000", first);
            Assert.Equal(first, second);
            Assert.Equal("63fc0000000000000000000000000000", otherPort);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ResolveDirectory_PrefersPersistentServerData()
    {
        Assert.Equal(
            Path.GetFullPath("C:/coop-data"),
            ServerTelemetryIdentity.ResolveDirectory("C:/coop-data", "C:/bannerlord-user", "C:/app"));
        Assert.Equal(
            Path.GetFullPath("C:/bannerlord-user"),
            ServerTelemetryIdentity.ResolveDirectory(null, "C:/bannerlord-user", "C:/app"));
    }

    [Fact]
    public void LoadOrCreate_RepairsMalformedIdentityFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "coop-telemetry-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "telemetry-server-id-4200.txt"), "not-a-guid");

            Assert.Equal(
                "74fd0000000000000000000000000000",
                ServerTelemetryIdentity.LoadOrCreate(
                    directory,
                    4200,
                    () => "74fd0000000000000000000000000000"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
