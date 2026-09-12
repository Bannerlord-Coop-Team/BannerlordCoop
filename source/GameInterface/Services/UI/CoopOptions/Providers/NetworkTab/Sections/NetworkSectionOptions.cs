using System.Text.Json.Serialization;

namespace GameInterface.Services.UI.CoopOptions.Providers.NetworkTab.Sections;

/// <summary>Persisted local movement upload and download limits.</summary>
public class NetworkSectionOptions
{
    [JsonPropertyName("movementUploadMiBPerSecond")]
    public double? MovementUploadMiBPerSecond { get; set; }

    [JsonPropertyName("movementDownloadMiBPerSecond")]
    public double? MovementDownloadMiBPerSecond { get; set; }

    public bool TryGetUpload(out double value) =>
        TryGetValue(MovementUploadMiBPerSecond, out value);

    public bool TryGetDownload(out double value) =>
        TryGetValue(MovementDownloadMiBPerSecond, out value);

    private static bool TryGetValue(double? configured, out double value)
    {
        value = configured.GetValueOrDefault();
        return configured.HasValue &&
               !double.IsNaN(value) &&
               !double.IsInfinity(value) &&
               value > 0d &&
               value <= LocalMovementBandwidth.MaximumMiBPerSecond;
    }
}
