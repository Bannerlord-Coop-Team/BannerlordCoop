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

    [Theory]
    [InlineData(5, 2, 5, 2, true)]
    [InlineData(4, 1, 4, 1, true)]
    [InlineData(2, 0, 3, 2, true)]
    [InlineData(4, 1, 4, 1, false)]
    public void Validate_DuplicateEntries_ShareAvailabilityAndPreserveXpOnce(
        int firstNumber, int firstWounded, int secondNumber, int secondWounded, bool preserveTroopXp)
    {
        var character = ObjectHelper.SkipConstructor<CharacterObject>();
        var requested = Roster(
            Element(character, firstNumber, firstWounded),
            Element(character, secondNumber, secondWounded));
        var availableElement = Element(character, 5, 2);
        availableElement.Xp = 60;
        var available = Roster(availableElement);

        var result = validator.Validate(requested, available, preserveTroopXp);

        AssertElement(result, character, 5, 2);
        Assert.Equal(preserveTroopXp ? 60 : 0, Assert.Single(result.GetTroopRoster()).Xp);
        AssertElement(available, character, 5, 2);
        Assert.Equal(60, Assert.Single(available.GetTroopRoster()).Xp);
    }

    private static TroopRoster Roster(params TroopRosterElement[] elements)
    {
        var roster = new TroopRoster();
        foreach (var element in elements)
        {
            int index = roster.AddNewElement(element.Character, -1);
            roster.data[index] = element;
        }
        roster.UpdateVersion();
        roster.InitializeCachedData();
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
