using Missions.Agents.Extensions;
using Xunit;

namespace Coop.Tests.Missions.Agents;

public class AgentExtensionsTests
{
    // Ram kills and drowning happen only on an agent's owner, so neither may touch another owner's puppet here.
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void IsPuppet_OnlyForANetworkedAgentThisClientDoesNotControl(bool isRegistered, bool isLocallyControlled, bool expected)
    {
        Assert.Equal(expected, AgentExtensions.IsPuppet(isRegistered, isLocallyControlled));
    }
}
