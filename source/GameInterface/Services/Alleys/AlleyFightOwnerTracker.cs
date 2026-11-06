using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Alleys;

/// <summary>Holds the alley owner from when this client's fight started until it sends the take over.</summary>
internal interface IAlleyFightOwnerTracker
{
    void Set(Hero owner);

    Hero Take();
}

/// <summary>Alley owner when this client's fight started, held until the take over screen's Done. Per client, since E2E clients share a process.</summary>
internal class AlleyFightOwnerTracker : IAlleyFightOwnerTracker
{
    private Hero ownerAtFightStart;

    public void Set(Hero owner)
    {
        ownerAtFightStart = owner;
    }

    public Hero Take()
    {
        var owner = ownerAtFightStart;
        ownerAtFightStart = null;
        return owner;
    }
}
