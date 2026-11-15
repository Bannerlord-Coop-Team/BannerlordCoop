using Missions.Sessions;

namespace E2E.Tests.Services.Sessions;

public class SessionInstanceIdTests
{
    [Fact]
    public void Create_HasPrefixAndIsUnique()
    {
        var first = SessionInstanceId.Create();
        var second = SessionInstanceId.Create();

        Assert.NotEqual(first, second);
        Assert.StartsWith("session:", first);
        Assert.StartsWith("session:", second);
    }

    [Fact]
    public void IsSession_RejectsMapEventAndLocationIds()
    {
        Assert.False(SessionInstanceId.IsSession("123"));
        Assert.False(SessionInstanceId.IsSession("12|34"));
        Assert.False(SessionInstanceId.IsSession(null));
        Assert.False(SessionInstanceId.IsSession(string.Empty));
        Assert.True(SessionInstanceId.IsSession(SessionInstanceId.Create()));
    }
}
