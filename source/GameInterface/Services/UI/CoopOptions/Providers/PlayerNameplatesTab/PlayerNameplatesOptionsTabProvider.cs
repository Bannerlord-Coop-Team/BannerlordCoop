using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab.Sections;
using System;

namespace GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab;

/// <summary>Creates the client nameplate tab when the server permits it.</summary>
public class PlayerNameplatesOptionsTabProvider : ICoopOptionsTabProvider
{
    public const string TabId = "PlayerNameplatesTab";
    public const string TabName = "Nameplates";

    public string Id => TabId;
    public bool IsAvailable(ModOptions modOptions) => modOptions.ShowPlayerNameplates;

    public CoopOptionsTabVM CreateTab(
        CoopOptionsData options,
        IMessageBroker messageBroker,
        Action<CoopOptionsTabVM> onSelect)
    {
        return new CoopOptionsTabVM(
            Id,
            TabName,
            new CoopOptionsSectionVM[]
            {
                new PlayerNameplatesSection(GetShowPlayerNameplatesOrDefault(options), messageBroker)
            },
            onSelect);
    }

    public static bool GetShowPlayerNameplatesOrDefault(CoopOptionsData options)
    {
        var sectionOptions = (options ?? new CoopOptionsData()).GetSectionOrDefault(
            TabId,
            PlayerNameplatesSection.SectionId,
            new PlayerNameplatesSectionOptions());
        return sectionOptions.GetShowPlayerNameplatesOrDefault();
    }
}
