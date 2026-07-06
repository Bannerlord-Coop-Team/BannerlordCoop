using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace GameInterface.Services.Villages.Interfaces;

internal static class VillageHostileFactionStanceHelper
{
    public static bool HasWarStance(IFaction faction1, IFaction faction2)
    {
        if (faction1 == null || faction2 == null || faction1 == faction2)
            return false;

        if (FactionManager.IsAtWarAgainstFaction(faction1, faction2))
            return true;

        return HasFactionWar(faction1, faction2) && HasFactionWar(faction2, faction1);
    }

    public static void ApplyWarStance(IFaction faction1, IFaction faction2)
    {
        if (faction1 == null || faction2 == null)
            return;

        if (FactionManager.IsAtWarAgainstFaction(faction1, faction2) == false)
            FactionManager.SetStance(faction1, faction2, StanceType.War);

        var stanceLink = FactionManager.Instance.GetStanceLinkInternal(faction1, faction2);
        if (stanceLink.StanceType != StanceType.War)
            stanceLink.StanceType = StanceType.War;

        EnsureStanceLinkRegistered(faction1, faction2, stanceLink);
        faction1.UpdateFactionsAtWarWith();
        faction2.UpdateFactionsAtWarWith();
        EnsureFactionAtWarWith(faction1, faction2);
        EnsureFactionAtWarWith(faction2, faction1);
    }

    private static void EnsureStanceLinkRegistered(IFaction faction1, IFaction faction2, StanceLink stanceLink)
    {
        var stances = FactionManager.Instance._stances._stances;
        stances[GetStanceKey(faction1, faction2)] = stanceLink;
    }

    private static (IFaction, IFaction) GetStanceKey(IFaction faction1, IFaction faction2)
    {
        if (faction1.Id < faction2.Id)
            return (faction1, faction2);

        return (faction2, faction1);
    }

    private static void EnsureFactionAtWarWith(IFaction faction, IFaction otherFaction)
    {
        var factionsAtWarWith = GetFactionsAtWarWith(faction);
        if (factionsAtWarWith == null || factionsAtWarWith.Contains(otherFaction))
            return;

        factionsAtWarWith.Add(otherFaction);
    }

    private static bool HasFactionWar(IFaction faction, IFaction otherFaction)
    {
        try
        {
            return faction.FactionsAtWarWith?.Contains(otherFaction) == true;
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }

    private static MBList<IFaction> GetFactionsAtWarWith(IFaction faction)
    {
        if (faction is Clan clan)
        {
            clan._factionsAtWarWith ??= new MBList<IFaction>();
            return clan._factionsAtWarWith;
        }

        if (faction is Kingdom kingdom)
        {
            kingdom._factionsAtWarWith ??= new MBList<IFaction>();
            return kingdom._factionsAtWarWith;
        }

        return null;
    }
}
