using GameInterface.Services.Registry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.ItemComponents;

/// <summary>
/// Registry for <see cref="ItemComponent"/> objects
/// </summary>
internal class ItemComponentRegistry : RegistryBase<ItemComponent>
{
    private const string CoopItemComponentPrefix = "CoopItemComponent";
    private static int InstanceCounter = 0;

    public ItemComponentRegistry(IRegistryCollection collection) : base(collection) { }

    public override IEnumerable<Type> ManagedTypes { get; } = new Type[]
    {
        typeof(HorseComponent),
        typeof(ArmorComponent),
        typeof(WeaponComponent),
        typeof(BannerComponent),
        typeof(SaddleComponent),
        typeof(TradeItemComponent),
    };

    public override void RegisterAll()
    {
        foreach (var component in Campaign.Current.AllItems.Select(p => p.ItemComponent).Where(c => c != null))
        {
            RegisterNewObject(component, out var _);
        }
    }

    protected override string GetNewId(ItemComponent party)
    {
        return $"{CoopItemComponentPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}

