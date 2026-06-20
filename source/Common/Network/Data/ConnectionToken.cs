using Common.Logging;
using Serilog;
using System;

namespace Common.Network.Data;

/// <summary>
/// Represents a connection token containing information required for connecting to a game instance.
/// </summary>
public class ConnectionToken
{
    private static readonly ILogger Logger = LogManager.GetLogger<ConnectionToken>();

    public string ControllerId { get; }
    public string InstanceId { get; }

    /// <summary>
    /// Initializes a new instance of the ConnectionToken class with the specified peer identifier, game instance name, and NAT type.
    /// </summary>
    /// <param name="peerId">The unique identifier of the peer associated with the connection token.</param>
    /// <param name="instanceName">The name of the game instance associated with the connection token.</param>
    /// <param name="natType">The type of the NAT associated with the connection token.</param>
    /// <exception cref="ArgumentException">Thrown when the peer identifier is null or empty or the instance name is null or empty.</exception>
    public ConnectionToken(string peerId, string instanceName)
    {
        if (string.IsNullOrEmpty(peerId))
        {
            throw new ArgumentException("PeerId cannot be null or empty", nameof(peerId));
        }

        if (string.IsNullOrEmpty(instanceName))
        {
            throw new ArgumentException("InstanceName cannot be null or empty", nameof(instanceName));
        }

        ControllerId = peerId;
        InstanceId = instanceName;
    }

    public static bool TryParse(string stringToken, out ConnectionToken connectionToken)
    {
        connectionToken = null;
        try
        {
            connectionToken = stringToken;
            return true;
        }
        catch (ArgumentException e)
        {
            Logger.Error("Unable to parse token: {err}", e);
        }

        return false;
    }

    public override bool Equals(object obj)
    {
        if (obj is ConnectionToken == false) return false;

        ConnectionToken token = (ConnectionToken)obj;

        if (token.InstanceId != InstanceId) return false;
        if (token.ControllerId != ControllerId) return false;

        return true;
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    public static implicit operator ConnectionToken(string tokenString)
    {
        string[] data = tokenString.Split('%');
        if (data.Length != 2)
        {
            throw new ArgumentException("Invalid data length, expected 2 but got " + data.Length, nameof(tokenString));
        }

        string peerId = data[0];
        if (string.IsNullOrEmpty(peerId))
        {
            throw new ArgumentException("Invalid PeerId in token string", nameof(tokenString));
        }

        string instanceName = data[1];
        if (string.IsNullOrEmpty(instanceName))
        {
            throw new ArgumentException("InstanceName cannot be null or empty", nameof(tokenString));
        }

        return new ConnectionToken(peerId, instanceName);
    }

    public static implicit operator string(ConnectionToken token)
    {
        return string.Join("%", token.ControllerId, token.InstanceId);
    }
}
