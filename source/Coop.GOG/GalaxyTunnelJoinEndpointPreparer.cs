using Common.Logging;
using Common.Network.Session;
using Serilog;
using System;
using System.Threading.Tasks;

namespace Coop.GOG;

/// <summary>Turns a GOG peer target into a local UDP endpoint for LiteNetLib.</summary>
public sealed class GalaxyTunnelJoinEndpointPreparer : ITunnelJoinEndpointPreparer
{
    private static readonly ILogger Logger = LogManager.GetLogger<GalaxyTunnelJoinEndpointPreparer>();

    private readonly IGalaxySdk sdk;
    private readonly object gate = new object();
    private ProviderTunnelClient tunnel;

    internal GalaxyTunnelJoinEndpointPreparer(IGalaxySdk sdk)
    {
        this.sdk = sdk ?? throw new ArgumentNullException(nameof(sdk));
    }

    public string Provider => GalaxySessionProvider.ProviderId;

    public Task<SessionJoinInfo> PrepareAsync(SessionJoinInfo info)
    {
        lock (gate)
        {
            TearDownLocked();
            if (!string.Equals(info.TunnelTarget.Provider, Provider, StringComparison.Ordinal))
                return Task.FromResult(info);

            ProviderTunnelClient client = null;
            try
            {
                client = new ProviderTunnelClient(new GalaxyDatagramTransport(sdk));
                client.Start(info.TunnelTarget);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GOG tunnel setup failed; falling back to the advertised address");
                client?.Dispose();
                return Task.FromResult(info);
            }

            tunnel = client;
            return Task.FromResult(new SessionJoinInfo
            {
                Version = info.Version,
                Address = "127.0.0.1",
                Port = client.LocalPort,
                TunnelTarget = info.TunnelTarget,
                DedicatedServer = info.DedicatedServer,
                ModVersion = info.ModVersion,
                PasswordRequired = info.PasswordRequired,
                ConnectedPlayers = info.ConnectedPlayers,
                Discoverable = info.Discoverable,
                Password = info.Password,
                Tunneled = true,
            });
        }
    }

    public void TearDown()
    {
        lock (gate)
        {
            TearDownLocked();
        }
    }

    private void TearDownLocked()
    {
        tunnel?.Dispose();
        tunnel = null;
    }
}
