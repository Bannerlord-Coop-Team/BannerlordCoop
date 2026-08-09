using Common.Logging;
using Common.Network.Session;
using Serilog;
using System;
using System.Threading.Tasks;

namespace Coop.Steam;

/// <summary>
/// Prepares a tunneled join: stands up the local pump connecting to the host's Steam
/// identity and returns its loopback endpoint for the client to dial. Returns immediately;
/// the Steam link finishes connecting in the background while the client's connect retries
/// and the join watchdog bound the wait.
/// </summary>
public class SteamTunnelJoinEndpointPreparer : ITunnelJoinEndpointPreparer
{
    private static readonly ILogger Logger = LogManager.GetLogger<SteamTunnelJoinEndpointPreparer>();

    private readonly object gate = new object();
    private ProviderTunnelClient tunnel;

    public string Provider => SteamSessionProvider.ProviderId;

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
                client = new ProviderTunnelClient(new SteamDatagramTransportAdapter(
                    new SteamNetworkingTunnelTransport(),
                    () => Steamworks.SteamUser.GetSteamID().m_SteamID));
                client.Start(info.TunnelTarget);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Steam tunnel setup failed; falling back to the advertised address");
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

    /// <summary>Closes the active tunnel; called when the session ends or the join fails.</summary>
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
