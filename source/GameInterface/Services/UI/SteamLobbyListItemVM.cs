using System;
using TaleWorlds.Library;

namespace GameInterface.Services.UI;

/// <summary>
/// Display data and selection command for one hosted standalone server lobby.
/// </summary>
public sealed class SteamLobbyListItemVM : ViewModel
{
    private readonly Action<ulong> onJoin;

    public SteamLobbyListItemVM(
        ulong lobbyId,
        int protocolVersion,
        string modVersion,
        bool passwordRequired,
        bool isCompatible,
        Action<ulong> onJoin)
    {
        LobbyId = lobbyId;
        ProtocolVersion = protocolVersion;
        ModVersion = modVersion ?? string.Empty;
        PasswordRequired = passwordRequired;
        IsCompatible = isCompatible;
        this.onJoin = onJoin ?? throw new ArgumentNullException(nameof(onJoin));
    }

    public ulong LobbyId { get; }
    public int ProtocolVersion { get; }
    public string ModVersion { get; }
    public bool PasswordRequired { get; }
    public bool IsCompatible { get; }

    [DataSourceProperty]
    public string LobbyText => $"Lobby {LobbyId}";

    [DataSourceProperty]
    public string VersionText => string.IsNullOrWhiteSpace(ModVersion)
        ? $"Protocol {ProtocolVersion}"
        : $"{ModVersion} (protocol {ProtocolVersion})";

    [DataSourceProperty]
    public string PasswordText => PasswordRequired ? "Password required" : "No password";

    [DataSourceProperty]
    public string CompatibilityText => IsCompatible ? "Compatible" : "Incompatible version";

    [DataSourceProperty]
    public bool IsJoinDisabled => !IsCompatible;

    public void ExecuteJoin()
    {
        if (!IsCompatible) return;

        onJoin(LobbyId);
    }
}
