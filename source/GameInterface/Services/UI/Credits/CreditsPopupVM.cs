using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.Credits;

/// <summary>
/// Backs the credits popup: a title above named sections (contributors, community, supporters)
/// and a close button. Section names come from <see cref="CreditsRoster"/>.
/// </summary>
public class CreditsPopupVM : ViewModel
{
    internal const string EmptySectionText = "Coming soon";

    private readonly Action close;

    public CreditsPopupVM(Action close)
    {
        this.close = close ?? throw new ArgumentNullException(nameof(close));
    }

    public string TitleText => "Credits";
    public string ContributorsHeaderText => "Contributors";
    public string CommunityHeaderText => "Community";
    public string SupportersHeaderText => "Supporters";
    public string ContributorsText => FormatNames(CreditsRoster.Contributors);
    public string CommunityText => FormatNames(CreditsRoster.Community);
    public string SupportersText => FormatNames(CreditsRoster.Supporters);
    public string CloseButtonText => "Close";

    public void ActionClose()
    {
        close();
    }

    private static string FormatNames(IReadOnlyList<string> names)
    {
        return names.Count == 0 ? EmptySectionText : string.Join("\n", names);
    }
}
