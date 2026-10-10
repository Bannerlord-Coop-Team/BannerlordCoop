#if DEBUG
using Autofac;
using Common;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Common.Commands;
using Coop.Core.Server.Connections.Messages;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Library;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Commands;

[Collection(ModInformationRoleCollection.Name)]
public class JoinBuildDebugCommandsTests
{
    private readonly ITestOutputHelper output;

    public JoinBuildDebugCommandsTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void ReportBuild_IsAProcessLifetimeAttributedCommand()
    {
        MethodInfo method = typeof(JoinBuildDebugCommands).GetMethod(
            nameof(JoinBuildDebugCommands.ReportBuild),
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.True(method.IsDefined(
            typeof(CommandLineFunctionality.CommandLineArgumentFunction),
            inherit: false));
    }

    [Fact]
    public void ReportBuild_SetThenClear_ChangesTheReportedBuild()
    {
        bool wasServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = false;

            JoinBuildDebugCommands.ReportBuild(new List<string> { "9.9.9+deadbeef" });
            Assert.Equal("9.9.9+deadbeef", JoinBuildDebugCommands.ReportedBuildVersion);

            string result = JoinBuildDebugCommands.ReportBuild(new List<string> { "clear" });
            Assert.Equal(ModInformation.BuildVersion, JoinBuildDebugCommands.ReportedBuildVersion);
            Assert.Contains(ModInformation.BuildVersion, result);
        }
        finally
        {
            JoinBuildDebugCommands.ResetReportedBuild();
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void ReportBuild_OnServerOrWithoutOneArgument_ChangesNothing()
    {
        bool wasServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = true;
            Assert.Equal(
                "report_build must be run on a client.",
                JoinBuildDebugCommands.ReportBuild(new List<string> { "9.9.9+deadbeef" }));

            ModInformation.IsServer = false;
            Assert.StartsWith("Usage:", JoinBuildDebugCommands.ReportBuild(new List<string>()));
            Assert.StartsWith("Usage:", JoinBuildDebugCommands.ReportBuild(new List<string> { "a", "b" }));

            Assert.Equal(ModInformation.BuildVersion, JoinBuildDebugCommands.ReportedBuildVersion);
        }
        finally
        {
            JoinBuildDebugCommands.ResetReportedBuild();
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void ValidateModuleState_SendsTheReportedBuild()
    {
        bool wasServer = ModInformation.IsServer;
        var clientComponent = new ClientTestComponent(output);
        var serverPeer = clientComponent.TestNetwork.CreatePeer();
        var clientLogic = clientComponent.Container.Resolve<IClientLogic>();
        try
        {
            ModInformation.IsServer = false;
            JoinBuildDebugCommands.ReportBuild(new List<string> { "9.9.9+deadbeef" });

            clientLogic.SetState<ValidateModuleState>();

            var validate = Assert.Single(
                clientComponent.TestNetwork.GetPeerMessages(serverPeer).OfType<NetworkModuleVersionsValidate>());
            Assert.Equal("9.9.9+deadbeef", validate.CoopBuildVersion);
        }
        finally
        {
            clientLogic.Dispose();
            JoinBuildDebugCommands.ResetReportedBuild();
            ModInformation.IsServer = wasServer;
        }
    }
}
#endif
