using Common.Network;
using Coop.Core;
using Xunit;

namespace Coop.Tests.Session;

/// <summary>
/// Covers the startup War Sails host refusal at the in-process host entry point.
/// </summary>
public class CoopartiveMultiplayerExperienceTests
{
    // The refusal must stop the host attempt before any of its setup runs; the over-long password would otherwise throw.
    [Fact]
    public void StartAsServer_WithAHostRefusal_StartsNothing()
    {
        using var experience = new CoopartiveMultiplayerExperience(standaloneServerProcess: true, hostRefusal: "refused");

        var exception = Record.Exception(() =>
            experience.StartAsServer("save", new string('x', ConnectionPassword.MaxLength + 1)));

        Assert.Null(exception);
        Assert.False(experience.Running);
    }
}
