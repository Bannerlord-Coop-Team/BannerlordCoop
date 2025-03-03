using Common;
using GameInterface.Registry;
using System;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans;

/// <summary>
/// Registry class that assosiates <see cref="Clan"/> and a <see cref="string"/> id
/// </summary>
internal class ClanRegistry : RegistryBase<Clan>
{
    private const string ClanStringIdPrefix = "CoopClan";
    private int InstanceCounter = 0;

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
            base.RegisterNewObject(clan, out var _);
        }
    }

    protected override string GetNewId(Clan clan)
    {
        return $"{ClanStringIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }

}
