using Common;
using GameInterface.Services.Registry;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans;

/// <summary>
/// Registry class that assosiates <see cref="Clan"/> and a <see cref="string"/> id
/// </summary>
internal interface IClanRegistry : IRegistry<Clan>
{
    bool RegisterClan(Clan clan);
    bool RemoveClan(Clan clan);
    void RegisterAllClans();
}

/// <inheritdoc cref="IClanRegistry"/>
internal class ClanRegistry : RegistryBase<Clan>, IClanRegistry
{
    public bool RegisterClan(Clan clan)
    {
        if (RegisterExistingObject(clan.StringId, clan) == false)
        {
            Logger.Warning("Unable to register clan: {object}", clan.Name);
            return false;
        }

        return true;
    }

    public bool RemoveClan(Clan clan)
    {
        return Remove(clan.StringId);
    }

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
            RegisterClan(clan);
        }
    }

    private const string ClanStringIdPrefix = "CoopClan";
    public override bool RegisterNewObject(Clan obj, out string id)
    {
        id = null;

        if (Campaign.Current?.CampaignObjectManager == null) return false;

        var newId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<Clan>(ClanStringIdPrefix);

        if (objIds.ContainsKey(newId)) return false;

        obj.StringId = newId;

        objIds.Add(newId, obj);

        id = newId;

        return true;
    }
}
