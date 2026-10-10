using Common.Logging;
using Serilog;
using System;

namespace Common.Network.Data;

/// <summary>
/// Represents a connection token containing information required for connecting to a game instance.
/// </summary>
public class ConnectionToken
{
    public const int MaxSerializedLength = 1024;

    private static readonly ILogger Logger = LogManager.GetLogger<ConnectionToken>();

    public string ControllerId { get; }
    public string InstanceId { get; }
    public Guid PeerCredential { get; }

    /// <summary>
    /// Initializes a new instance of the ConnectionToken class with the specified peer identifier, game instance name, and NAT type.
    /// </summary>
    /// <param name="peerId">The unique identifier of the peer associated with the connection token.</param>
    /// <param name="instanceName">The name of the game instance associated with the connection token.</param>
    /// <param name="natType">The type of the NAT associated with the connection token.</param>
    /// <exception cref="ArgumentException">Thrown when the peer identifier is null or empty or the instance name is null or empty.</exception>
    public ConnectionToken(string peerId, string instanceName)
        : this(peerId, instanceName, Guid.Empty)
    {
    }

    public ConnectionToken(string peerId, string instanceName, Guid peerCredential)
    {
        if (string.IsNullOrEmpty(peerId))
        {
            throw new ArgumentException("PeerId cannot be null or empty", nameof(peerId));
        }

        if (string.IsNullOrEmpty(instanceName))
        {
            throw new ArgumentException("InstanceName cannot be null or empty", nameof(instanceName));
        }

        var credentialLength = peerCredential == Guid.Empty ? 0 : 33;
        if (peerId.Length + instanceName.Length + 1 + credentialLength > MaxSerializedLength)
        {
            throw new ArgumentException("The serialized connection token is too long");
        }

        ControllerId = peerId;
        InstanceId = instanceName;
        PeerCredential = peerCredential;
    }

    public static bool TryParse(string stringToken, out ConnectionToken connectionToken)
    {
        connectionToken = null;
        if (string.IsNullOrEmpty(stringToken) || stringToken.Length > MaxSerializedLength) return false;

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
        if (token.PeerCredential != PeerCredential) return false;

        return true;
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    public static implicit operator ConnectionToken(string tokenString)
    {
        string[] data = tokenString.Split('%');
        if (data.Length != 2 && data.Length != 3)
        {
            throw new ArgumentException("Invalid data length, expected 2 or 3 but got " + data.Length, nameof(tokenString));
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

        Guid peerCredential = Guid.Empty;
        if (data.Length == 3 &&
            !Guid.TryParseExact(data[2], "N", out peerCredential))
        {
            throw new ArgumentException("Invalid peer credential", nameof(tokenString));
        }

        return new ConnectionToken(peerId, instanceName, peerCredential);
    }

    public static implicit operator string(ConnectionToken token)
    {
        if (token.PeerCredential == Guid.Empty)
            return string.Join("%", token.ControllerId, token.InstanceId);

        return string.Join(
            "%",
            token.ControllerId,
            token.InstanceId,
            token.PeerCredential.ToString("N"));
    }
}
