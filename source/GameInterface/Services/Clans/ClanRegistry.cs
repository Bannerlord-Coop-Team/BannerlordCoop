using Common;
using GameInterface.Services.Registry;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans;

/// <summary>
/// Registry class that assosiates <see cref="Clan"/> and a <see cref="string"/> id
/// </summary>
internal interface IClanRegistry : IRegistry<Clan>
{
    void RegisterAllClans();
}

/// <inheritdoc cref="IClanRegistry"/>
internal class ClanRegistry : RegistryBase<Clan>, IClanRegistry
{
    public void RegisterAllClans()
    {
        var objectManager = Campaign.Current?.CampaignObjectManager;

        if (objectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }

        foreach (var clan in objectManager.Clans)
        {
            RegisterExistingObject(clan.StringId, clan);
        }
    }

    private const string ClanStringIdPrefix = "CoopClan";
    protected override string GetNewId(Clan party)
    {
        party.StringId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<Clan>(ClanStringIdPrefix);
        return party.StringId;
    }

}
