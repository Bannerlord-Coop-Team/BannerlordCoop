using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services.UI.CoopOptions.Providers.NetworkTab.Sections;
using System;

namespace GameInterface.Services.UI.CoopOptions.Providers.NetworkTab;

/// <summary>Builds the local network options tab.</summary>
public class NetworkOptionsTabProvider : ICoopOptionsTabProvider
{
    public const string TabId = "NetworkTab";
    public const string TabName = "Network";
    public const string SectionTitleText = "Movement Bandwidth";
    public const string SectionDescriptionText =
        "Limits compressed movement data uploaded and downloaded by this client. Changes apply when the next mission opens.";
    public const string UploadText = "Upload limit";
    public const string DownloadText = "Download limit";
    public const string UnitText = "MiB/s";

    public string Id => TabId;

    public bool IsAvailable(ModOptions modOptions) => true;

    public CoopOptionsTabVM CreateTab(
        CoopOptionsData options,
        IMessageBroker messageBroker,
        Action<CoopOptionsTabVM> onSelect)
    {
        NetworkSectionOptions sectionOptions = null;
        options?.TryGetSection(TabId, NetworkSection.SectionId, out sectionOptions);

        double upload = LocalMovementBandwidth.DefaultMiBPerSecond;
        double download = LocalMovementBandwidth.DefaultMiBPerSecond;
        if (sectionOptions != null)
        {
            if (sectionOptions.TryGetUpload(out double savedUpload))
                upload = savedUpload;
            if (sectionOptions.TryGetDownload(out double savedDownload))
                download = savedDownload;
        }

        return new CoopOptionsTabVM(
            Id,
            TabName,
            new CoopOptionsSectionVM[]
            {
                new NetworkSection(upload, download),
            },
            onSelect);
    }
}
