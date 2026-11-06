using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Alleys;

/// <summary>Holds the alley owner this client beat until it sends the take over.</summary>
internal interface IAlleyFightOwnerTracker
{
    void Set(Hero owner);

    Hero Take();
}

/// <summary>Alley owner this client beat, held until the take over screen's Done. Per client, since E2E clients share a process.</summary>
internal class AlleyFightOwnerTracker : IAlleyFightOwnerTracker
{
    private Hero ownerWhenFightWon;

    public void Set(Hero owner)
    {
        ownerWhenFightWon = owner;
    }

    public Hero Take()
    {
        var owner = ownerWhenFightWon;
        ownerWhenFightWon = null;
        return owner;
    }
}
