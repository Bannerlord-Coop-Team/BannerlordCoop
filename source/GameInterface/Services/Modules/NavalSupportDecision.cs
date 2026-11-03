namespace GameInterface.Services.Modules;

/// <summary>
/// The startup War Sails decision: whether naval co-op runs, and why the hosting side must refuse to host.
/// </summary>
public readonly struct NavalSupportDecision
{
    // Pairs the naval result with the hosting refusal, if any.
    public NavalSupportDecision(bool isActive, string hostRefusal = null)
    {
        IsActive = isActive;
        HostRefusal = hostRefusal;
    }

    /// <summary>The value for <see cref="Common.ModInformation.IsNavalDlcActive"/>.</summary>
    public bool IsActive { get; }

    /// <summary>Null unless the hosting side must refuse to host; names the mismatch and how to fix it.</summary>
    public string HostRefusal { get; }

    /// <summary>Whether the hosting side must refuse to host.</summary>
    public bool RefusesToHost => HostRefusal != null;
}
