using Common.Util;
using GameInterface.Services.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Services.Party;

/// <summary>
/// Tests authoritative prisoner sale validation.
/// </summary>
public class PrisonerSaleValidatorTests
{
    private readonly PrisonerSaleValidator validator = new();

    [Fact]
    public void Validate_RequestedPrisonersAvailable_ReturnsRequestedRoster()
    {
        var character = ObjectHelper.SkipConstructor<CharacterObject>();
        var requested = Roster(Element(character, 4, 1));
        var available = Roster(Element(character, 7, 3));

        var result = validator.Validate(requested, available);

        AssertElement(result, character, 4, 1);
    }

    [Fact]
    public void Validate_PrisonersAlreadyRemoved_ReturnsEmptyRoster()
    {
        var character = ObjectHelper.SkipConstructor<CharacterObject>();
        var requested = Roster(Element(character, 4, 1));
        var available = Roster();

        var result = validator.Validate(requested, available);

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void Validate_FewerPrisonersAvailable_ClampsTotal()
    {
        var character = ObjectHelper.SkipConstructor<CharacterObject>();
        var requested = Roster(Element(character, 8, 2));
        var available = Roster(Element(character, 3, 1));

        var result = validator.Validate(requested, available);

        AssertElement(result, character, 3, 1);
    }

    [Fact]
    public void Validate_HealthyAndWoundedAvailability_ClampsIndependently()
    {
        var character = ObjectHelper.SkipConstructor<CharacterObject>();
        var requested = Roster(Element(character, 6, 4));
        var available = Roster(Element(character, 5, 1));

        var result = validator.Validate(requested, available);

        AssertElement(result, character, 3, 1);
    }

    private static TroopRoster Roster(params TroopRosterElement[] elements)
    {
        var roster = new TroopRoster();
        foreach (var element in elements)
        {
            roster.AddToCounts(
                element.Character,
                element.Number,
                false,
                element.WoundedNumber,
                element.Xp,
                true);
        }
        return roster;
    }

    private static TroopRosterElement Element(
        CharacterObject character,
        int number,
        int woundedNumber) =>
        new(character)
        {
            Number = number,
            WoundedNumber = woundedNumber,
        };

    private static void AssertElement(
        TroopRoster roster,
        CharacterObject character,
        int number,
        int woundedNumber)
    {
        var element = Assert.Single(roster.GetTroopRoster());
        Assert.Same(character, element.Character);
        Assert.Equal(number, element.Number);
        Assert.Equal(woundedNumber, element.WoundedNumber);
    }
}
