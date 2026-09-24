using TaleWorlds.PlatformService;

namespace GameInterface.Services.UI.PlayerList;

/// <summary>Provides the local platform display name independently of the network transport.</summary>
public interface IPlatformDisplayNameProvider : IGameAbstraction
{
    string GetName();
}

/// <inheritdoc cref="IPlatformDisplayNameProvider"/>
public sealed class PlatformDisplayNameProvider : IPlatformDisplayNameProvider
{
    // Uses Bannerlord's selected platform rather than assuming every client is on Steam.
    public string GetName() => PlatformServices.Instance.UserDisplayName ?? string.Empty;
}
