using GameInterface.Services.Registry;
using System;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Armies;

/// <summary>
/// Registry for <see cref="TroopRoster"/> type
/// </summary>
internal class TroopRosterRegistry : RegistryBase<TroopRoster>
{
    private const string TroopRosterIdPrefix = "CoopTroopRoster";
    private static int InstanceCounter = 0;

    public TroopRosterRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        foreach(MobileParty party in Campaign.Current.MobileParties)
        {
            if (RegisterNewObject(party.MemberRoster, out var _) == false)
            {
                Logger.Error($"Unable to register {party.MemberRoster}");
            }
        }
    }

    protected override string GetNewId(TroopRoster obj)
    {
        return $"{TroopRosterIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}
