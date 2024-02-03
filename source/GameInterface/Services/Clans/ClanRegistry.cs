﻿using Common;
using GameInterface.Services.Registry;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans;

/// <summary>
/// Registry class that assosiates <see cref="Clan"/> and a <see cref="string"/> id
/// </summary>
internal class ClanRegistry : RegistryBase<Clan>
{
    private const string ClanStringIdPrefix = "CoopClan";

    public ClanRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
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

    protected override string GetNewId(Clan party)
    {
        party.StringId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<Clan>(ClanStringIdPrefix);
        return party.StringId;
    }

}
