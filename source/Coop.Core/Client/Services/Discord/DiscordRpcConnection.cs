using DiscordRPC;
using DiscordRPC.Message;
using System;

namespace Coop.Core.Client.Services.Discord;

public interface IDiscordRpcConnection : IDisposable
{
    event Action Ready;
    bool Initialize();
    void SetPresence(RichPresence presence);
}

/// <summary>Exposes the library's post-synchronization READY callback.</summary>
public sealed class DiscordRpcConnection : IDiscordRpcConnection
{
    private DiscordRpcClient client;
    public event Action Ready;

    public bool Initialize()
    {
        client = new DiscordRpcClient(DiscordPresenceClient.ApplicationId) { SkipIdenticalPresence = false };
        client.OnReady += Handle_Ready;
        return client.Initialize();
    }

    private void Handle_Ready(object sender, ReadyMessage message) => Ready?.Invoke();

    public void SetPresence(RichPresence presence) => client.SetPresence(presence);

    public void Dispose()
    {
        if (client == null) return;
        client.OnReady -= Handle_Ready;
        client.Dispose();
    }
}
