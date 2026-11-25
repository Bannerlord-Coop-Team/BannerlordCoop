using Missions;
using Missions.Agents.Handlers;
using Missions.Agents.Packets;
using System;
using Xunit;

namespace Coop.Tests.Missions.Agents;

public class PilotSeatAttachmentTests
{
    [Theory]
    [InlineData(false, "owner", 3, true)]
    [InlineData(true, "owner", 3, false)]
    [InlineData(false, "new-owner", 3, false)]
    [InlineData(false, "owner", 4, false)]
    public void MatchesAgentAuthority_RequiresTheSameRegistrationOwnerAndRevision(
        bool reregistered, string controller, long revision, bool expected)
    {
        var agentId = Guid.NewGuid();
        var original = new CoopAgentInfo("owner", "owner", "owner", null, agentId, 1, 3);
        var seat = new RemoteAgentActionProcessor.PilotSeatAttachment
        {
            Info = original,
            ControllerId = "owner",
            Data = new AgentPilotSeatData("battle", 1, 2, 5, 7, 3, usingSeat: true),
        };
        var registered = reregistered
            ? new CoopAgentInfo("owner", "owner", "owner", null, agentId, 1, 3)
            : original;
        registered.CurrentAuthority = controller;
        registered.AuthorityRevision = revision;

        Assert.Equal(expected, seat.MatchesAgentAuthority(registered));
    }

    [Theory]
    [InlineData("owner", 5, 7, true)]
    [InlineData("new-owner", 5, 7, false)]
    [InlineData("owner", 6, 7, false)]
    [InlineData("owner", 5, 8, false)]
    public void MatchesMachineAuthority_RequiresTheSameOwnerEpochAndRevision(
        string owner, int epoch, int revision, bool expected)
    {
        var seat = new RemoteAgentActionProcessor.PilotSeatAttachment
        {
            ControllerId = "owner",
            Data = new AgentPilotSeatData("battle", 1, 2, 5, 7, 3, usingSeat: true),
        };

        Assert.Equal(expected, seat.MatchesMachineAuthority(owner, epoch, revision));
    }
}
