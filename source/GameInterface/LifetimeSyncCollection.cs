using System;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface;
internal static class LifetimeSyncCollection
{
    public static Type[] LifetimeSync = new Type[]
    {
        //typeof(LifetimeSync<>).MakeGenericType(typeof(SiegeEvent)),
    };
}
