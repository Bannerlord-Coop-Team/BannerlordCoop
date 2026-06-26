using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace GameInterface.Services.Stances
{
    internal static class FactionStanceHelper
    {
        private static readonly FieldInfo ClanFactionsAtWarWithField = AccessTools.Field(typeof(Clan), "_factionsAtWarWith");
        private static readonly FieldInfo KingdomFactionsAtWarWithField = AccessTools.Field(typeof(Kingdom), "_factionsAtWarWith");
        private static readonly FieldInfo FactionManagerStancesField = AccessTools.Field(typeof(FactionManager), "_stances");
        private static readonly FieldInfo StancesDataDictionaryField = AccessTools.Field(typeof(FactionManagerStancesData), "_stances");

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
            var stancesData = FactionManagerStancesField.GetValue(FactionManager.Instance);
            var stances = (Dictionary<(IFaction, IFaction), StanceLink>)StancesDataDictionaryField.GetValue(stancesData);
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

        private static MBList<IFaction> GetFactionsAtWarWith(IFaction faction)
        {
            FieldInfo field;
            object owner;
            if (faction is Clan clan)
            {
                field = ClanFactionsAtWarWithField;
                owner = clan;
            }
            else if (faction is Kingdom kingdom)
            {
                field = KingdomFactionsAtWarWithField;
                owner = kingdom;
            }
            else
            {
                return null;
            }

            var factionsAtWarWith = (MBList<IFaction>)field.GetValue(owner);
            if (factionsAtWarWith != null)
                return factionsAtWarWith;

            factionsAtWarWith = new MBList<IFaction>();
            field.SetValue(owner, factionsAtWarWith);
            return factionsAtWarWith;
        }
    }
}