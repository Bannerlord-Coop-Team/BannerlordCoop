namespace GameInterface.Services.UI;

/// <summary>Locally persisted values for the direct connection form.</summary>
internal sealed class DirectConnectionOptions
{
    public bool RememberConnection { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
