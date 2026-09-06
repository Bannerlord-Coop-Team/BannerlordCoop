using GameInterface.Services.UI.CoopOptions.Providers.NetworkTab.Sections;
using System;

namespace GameInterface.Services.UI.CoopOptions.Providers.NetworkTab;

/// <summary>Provides this client's saved movement bandwidth overrides.</summary>
public interface ILocalMovementBandwidth
{
    double? UploadMiBPerSecond { get; }
    double? DownloadMiBPerSecond { get; }
}

/// <summary>Reads this client's saved movement bandwidth overrides.</summary>
public class LocalMovementBandwidth : ILocalMovementBandwidth
{
    public const double DefaultMiBPerSecond = 5d;
    public const double MaximumMiBPerSecond = 1024d;

    public double? UploadMiBPerSecond { get; }
    public double? DownloadMiBPerSecond { get; }

    public LocalMovementBandwidth(ICoopOptionsStore optionsStore)
    {
        if (optionsStore == null) throw new ArgumentNullException(nameof(optionsStore));

        CoopOptionsData options = optionsStore.LoadOrDefault();
        if (!options.TryGetSection(
                NetworkOptionsTabProvider.TabId,
                NetworkSection.SectionId,
                out NetworkSectionOptions sectionOptions))
        {
            return;
        }

        if (sectionOptions.TryGetUpload(out double upload))
            UploadMiBPerSecond = upload;
        if (sectionOptions.TryGetDownload(out double download))
            DownloadMiBPerSecond = download;
    }
}
