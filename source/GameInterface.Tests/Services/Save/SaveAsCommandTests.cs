using Common;
using Common.Commands;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Save.Commands;
using Moq;
using System;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Services.Save;

public class SaveAsCommandTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void ReportsSuccessOnlyForCompletedPair(bool nativeSuccess, bool sidecarWritten, bool expected)
    {
        bool previous = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            bool written = false;
            var saves = new Mock<ISaveInterface>();
            saves.SetupGet(x => x.CanQueueSave).Returns(true);
            saves.Setup(x => x.ReadSaveFile(It.IsAny<string>())).Returns((string name) =>
                written && (name.EndsWith(".sav") || sidecarWritten) ? new byte[] { 1 } : null);
            saves.Setup(x => x.SaveCurrentGameToFile("proof")).Returns(() =>
            {
                written = true;
                return new SaveResults(nativeSuccess, new byte[] { 1 }, "campaign");
            });
            var args = new Mock<ICoopCommandArgs>();
            args.Setup(x => x[0]).Returns("proof");
            var result = new SaveDebugCommand.SaveSaveAsCoopCommand(saves.Object).ProcessCommand(args.Object);
            Assert.Equal(expected, result.Succeeded);
            saves.Verify(x => x.SaveCurrentGameToFile("proof"), Times.Once);
        }
        finally { ModInformation.IsServer = previous; }
    }

    [Fact]
    public void ExistingSaveIsNotOverwritten()
    {
        bool previous = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            var saves = new Mock<ISaveInterface>();
            saves.SetupGet(x => x.CanQueueSave).Returns(true);
            saves.Setup(x => x.ReadSaveFile("proof.sav")).Returns(new byte[] { 1 });
            var args = new Mock<ICoopCommandArgs>();
            args.Setup(x => x[0]).Returns("proof");
            Assert.False(new SaveDebugCommand.SaveSaveAsCoopCommand(saves.Object).ProcessCommand(args.Object).Succeeded);
            saves.Verify(x => x.SaveCurrentGameToFile(It.IsAny<string>()), Times.Never);
        }
        finally { ModInformation.IsServer = previous; }
    }
}