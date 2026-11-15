using Common.Messaging;
using Common.Network.Session;
using Common.Tests.Utils;
using GameInterface.Services.Sessions;
using GameInterface.Services.Sessions.Messages;
using GameInterface.Services.UI.Messages;
using System;
using System.Linq;
using System.Net;
using Xunit;

namespace GameInterface.Tests.Services.Sessions;

[Collection(ContainerProviderCollection.Name)]
public class SessionLauncherTests
{
    private readonly TestMessageBroker broker = new TestMessageBroker();

    [Fact]
    public void Host_PublishesAttemptHostSession()
    {
        using var launcher = new SessionLauncher(_ => { }, broker);

        launcher.Host("test.mod", new byte[] { 1, 2 }, "pw", ServerVisibility.FriendsOnly);

        var message = Assert.Single(broker.GetMessagesFromType<AttemptHostSession>());
        Assert.Equal("test.mod", message.ModId);
        Assert.Equal(new byte[] { 1, 2 }, message.Payload);
        Assert.Equal("pw", message.Password);
        Assert.Equal(ServerVisibility.FriendsOnly, message.Visibility);
    }

    [Fact]
    public void Host_RejectsOversizedPayload()
    {
        using var launcher = new SessionLauncher(_ => { }, broker);

        Assert.Throws<ArgumentException>(() => launcher.Host("test.mod", new byte[AttemptHostSession.MaxPayloadBytes + 1], null, ServerVisibility.Public));
        Assert.Throws<ArgumentException>(() => launcher.Host(null, new byte[0], null, ServerVisibility.Public));
        Assert.Empty(broker.GetMessagesFromType<AttemptHostSession>());
    }

    [Fact]
    public void Join_PublishesAttemptJoin()
    {
        using var launcher = new SessionLauncher(_ => { }, broker);

        launcher.Join(IPAddress.Loopback, 4200, "pw");

        var message = Assert.Single(broker.GetMessagesFromType<AttemptJoin>());
        Assert.Equal(4200, message.Port);
        Assert.Equal("pw", message.Password);
    }

    [Fact]
    public void OpenRequested_InvokesCallback()
    {
        SessionMissionOpenRequested received = null;
        using var launcher = new SessionLauncher(request => received = request, broker);

        broker.Publish(this, new SessionMissionOpenRequested("session:abc", "test.mod", new byte[] { 7 }));

        Assert.Equal("session:abc", received.SessionId);
        Assert.Equal(new byte[] { 7 }, received.Payload);
    }

    [Fact]
    public void HostChanged_RaisesEvent()
    {
        SessionHostChanged received = null;
        using var launcher = new SessionLauncher(_ => { }, broker);
        launcher.HostChanged += changed => received = changed;

        broker.Publish(this, new SessionHostChanged("session:abc", "host", 2, true));

        Assert.Equal("host", received.HostControllerId);
        Assert.Equal(2, received.Epoch);
        Assert.True(received.IsLocalHost);
    }

    [Fact]
    public void Dispose_Unsubscribes()
    {
        var launcher = new SessionLauncher(_ => { }, broker);
        Assert.True(broker.GetSubscriberCountForType<SessionHostChanged>() > 0);

        launcher.Dispose();

        Assert.True(broker.GetSubscriberCountForType<SessionHostChanged>() <= 0);
        Assert.True(broker.GetSubscriberCountForType<SessionMissionOpenRequested>() <= 0);
        Assert.True(broker.GetSubscriberCountForType<SessionMemberJoined>() <= 0);
        Assert.True(broker.GetSubscriberCountForType<SessionMissionEndRequested>() <= 0);
    }

    [Fact]
    public void NoContainer_CreateMissionBehaviors_Empty()
    {
        using var launcher = new SessionLauncher(_ => { }, broker);

        Assert.Empty(launcher.CreateMissionBehaviors("session:abc"));
        Assert.False(launcher.Close());
        Assert.False(launcher.IsLocalHost);
        Assert.False(launcher.Channel.SendToSession(null));
    }
}
