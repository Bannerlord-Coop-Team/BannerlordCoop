using Common;
using GameInterface.Services.Party.Commands;
using System;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.Party;

/// <summary>
/// Added a regression test to check first if the instance is a server and
/// if that is true, to return the message.
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public class PartyCommandsTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;

    public void Dispose()
    {
        ModInformation.IsServer = wasServer;
    }

    [Fact]
    public void WhoAmI_WhenServer_ReturnsClientOnlyError()
    {
        ModInformation.IsServer = true;

        var result = PartyCommands.WhoAmICommand(new List<string>());
        
        Assert.Equal("Command can only be run on a client.", result);
    }
}