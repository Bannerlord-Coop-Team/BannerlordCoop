using Missions.Agents.Handlers;

namespace E2E.Tests.Environment.MockEngine;

public sealed class MockAnimationActionCountProvider :
    IAnimationActionCountProvider
{
    public int GetActionCount()
    {
        return 10000;
    }
}
