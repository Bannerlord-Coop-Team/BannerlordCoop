using GameInterface.Services.Registry;
using System;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Monsters;

/// <summary>
/// Registry for <see cref="Monster"/> type
/// </summary>
internal class MonsterRegistry : RegistryBase<Monster>
{
    private const string MonsterIdPrefix = "CoopMonster";
    private static int InstanceCounter = 0;

    public MonsterRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        foreach (Monster monster in MBObjectManager.Instance.GetObjectTypeList<Monster>())
        {
            RegisterNewObject(monster, out _);
        }
    }

    protected override string GetNewId(Monster obj)
    {
        return obj.StringId;
    }
}
