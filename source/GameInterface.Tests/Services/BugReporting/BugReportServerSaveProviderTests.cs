using Common.Messaging;
using GameInterface.Services.BugReporting;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using Moq;
using Serilog;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace GameInterface.Tests.Services.BugReporting;

public class BugReportServerSaveProviderTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(
        Path.GetTempPath(), "BugReportServerSaveProviderTests_" + Guid.NewGuid().ToString("N"));
    private readonly byte[] campaignData = Encoding.UTF8.GetBytes("current campaign save");
    private readonly byte[] currentSidecarData = Encoding.UTF8.GetBytes("{\"players\":[\"current\"]}");
    private string SidecarPath => Path.Combine(tempRoot, "coop_bug_report.json");

    public BugReportServerSaveProviderTests()
    {
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(SidecarPath, "{\"players\":[\"previous\"]}");
    }

    [Fact]
    public void TryCapture_PreviousSidecarAndSuppressedCurrentWriteFailure_OmitsSidecar()
    {
        using var broker = new MessageBroker();
        using var logger = new LoggerConfiguration().CreateLogger();
        var writeFailed = false;
        Action<MessagePayload<GameSaved>> writeSidecar = _ =>
        {
            using var lockedFile = new FileStream(SidecarPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try
            {
                File.WriteAllBytes(SidecarPath, currentSidecarData);
            }
            catch (IOException)
            {
                writeFailed = true;
                throw;
            }
        };
        broker.Subscribe(writeSidecar);
        var saveInterface = CreateSaveInterface(() => broker.Publish(this, new GameSaved(BugReportServerSaveProvider.SaveName)));
        var provider = new BugReportServerSaveProvider(saveInterface.Object, logger);

        var captured = provider.TryCapture(out var save);

        Assert.True(writeFailed);
        Assert.True(captured);
        Assert.Equal(campaignData, save.Data);
        Assert.Null(save.SidecarFileName);
        Assert.Null(save.SidecarData);
        GC.KeepAlive(writeSidecar);
    }

    [Fact]
    public void TryCapture_PreviousSidecarAndSuccessfulWrite_ReturnsCurrentPair()
    {
        var saveInterface = CreateSaveInterface(() =>
        {
            Assert.False(File.Exists(SidecarPath));
            File.WriteAllBytes(SidecarPath, currentSidecarData);
        });
        var provider = new BugReportServerSaveProvider(saveInterface.Object, Mock.Of<ILogger>());

        var captured = provider.TryCapture(out var save);

        Assert.True(captured);
        Assert.Equal("coop_bug_report.sav", save.FileName);
        Assert.Equal(campaignData, save.Data);
        Assert.Equal("coop_bug_report.json", save.SidecarFileName);
        Assert.Equal(currentSidecarData, save.SidecarData);
        Assert.Equal(currentSidecarData, File.ReadAllBytes(SidecarPath));
    }

    [Fact]
    public void TryCapture_PreviousSidecarCannotBeDeleted_DoesNotSaveOrReadSidecar()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        var saveInterface = CreateSaveInterface(() => File.WriteAllBytes(SidecarPath, currentSidecarData));
        saveInterface.Setup(value => value.DeleteSaveFile("coop_bug_report.json"))
            .Throws(new IOException("Sidecar is locked"));
        var provider = new BugReportServerSaveProvider(saveInterface.Object, logger);

        var captured = provider.TryCapture(out var save);

        Assert.False(captured);
        Assert.Null(save);
        Assert.Equal("{\"players\":[\"previous\"]}", File.ReadAllText(SidecarPath));
        saveInterface.Verify(value => value.SaveCurrentGameToFile(It.IsAny<string>()), Times.Never);
        saveInterface.Verify(value => value.ReadSaveFile(It.IsAny<string>()), Times.Never);
    }

    private Mock<ISaveInterface> CreateSaveInterface(Action saveSidecar)
    {
        var saveInterface = new Mock<ISaveInterface>(MockBehavior.Strict);
        saveInterface.Setup(value => value.DeleteSaveFile("coop_bug_report.json"))
            .Callback(() => File.Delete(SidecarPath));
        saveInterface.Setup(value => value.SaveCurrentGameToFile(BugReportServerSaveProvider.SaveName))
            .Callback(saveSidecar)
            .Returns(new SaveResults(true, campaignData, "campaign-id"));
        saveInterface.Setup(value => value.ReadSaveFile("coop_bug_report.json"))
            .Returns(() => File.Exists(SidecarPath) ? File.ReadAllBytes(SidecarPath) : null);
        return saveInterface;
    }

    public void Dispose()
    {
        Directory.Delete(tempRoot, recursive: true);
    }
}
