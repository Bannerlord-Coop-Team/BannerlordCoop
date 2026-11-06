using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Alleys;

internal interface IAlleyFightOwnerTracker
{
    void Set(Hero owner);

    Hero Take();
}

/// <summary>
/// Holds who owned the alley when this client won its alley fight, until the take over screen's done button
/// sends the request. Another player's take over can reach this client while it sits in the troop screen,
/// and the server must judge the request by the owner this player beat. Kept per client instead of in a
/// static, since the E2E clients share one process.
/// </summary>
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
