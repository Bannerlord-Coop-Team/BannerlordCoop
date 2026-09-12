using TaleWorlds.Library;

namespace GameInterface.Services.UI.CoopOptions.Providers.NetworkTab.Sections;

/// <summary>Edits this client's movement bandwidth limits.</summary>
public class NetworkSection : CoopOptionsSectionVM
{
    public const string SectionId = "NetworkSection";
    public const float MinimumMiBPerSecond = 0.01f;
    public const float MaximumMiBPerSecond = (float)LocalMovementBandwidth.MaximumMiBPerSecond;

    private float movementUploadMiBPerSecond;
    private float movementDownloadMiBPerSecond;

    public NetworkSection(double movementUploadMiBPerSecond, double movementDownloadMiBPerSecond)
    {
        this.movementUploadMiBPerSecond = Clamp(movementUploadMiBPerSecond);
        this.movementDownloadMiBPerSecond = Clamp(movementDownloadMiBPerSecond);
    }

    public override string Id => SectionId;

    public string TitleText => NetworkOptionsTabProvider.SectionTitleText;
    public string DescriptionText => NetworkOptionsTabProvider.SectionDescriptionText;
    public string UploadText => NetworkOptionsTabProvider.UploadText;
    public string DownloadText => NetworkOptionsTabProvider.DownloadText;
    public string UnitText => NetworkOptionsTabProvider.UnitText;

    [DataSourceProperty]
    public float MovementUploadMiBPerSecond
    {
        get => movementUploadMiBPerSecond;
        set
        {
            float clamped = Clamp(value);
            if (movementUploadMiBPerSecond == clamped) return;

            movementUploadMiBPerSecond = clamped;
            OnPropertyChanged(nameof(MovementUploadMiBPerSecond));
        }
    }

    [DataSourceProperty]
    public float MovementDownloadMiBPerSecond
    {
        get => movementDownloadMiBPerSecond;
        set
        {
            float clamped = Clamp(value);
            if (movementDownloadMiBPerSecond == clamped) return;

            movementDownloadMiBPerSecond = clamped;
            OnPropertyChanged(nameof(MovementDownloadMiBPerSecond));
        }
    }

    public override void Apply(string tabId, CoopOptionsData options)
    {
        options.SetSection(tabId, Id, new NetworkSectionOptions
        {
            MovementUploadMiBPerSecond = movementUploadMiBPerSecond,
            MovementDownloadMiBPerSecond = movementDownloadMiBPerSecond,
        });
    }

    private static float Clamp(double value)
    {
        if (double.IsNaN(value) || double.IsNegativeInfinity(value))
            return MinimumMiBPerSecond;
        if (double.IsPositiveInfinity(value))
            return MaximumMiBPerSecond;

        return (float)System.Math.Max(
            MinimumMiBPerSecond,
            System.Math.Min(MaximumMiBPerSecond, value));
    }
}
