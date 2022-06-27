using Coop.Mod.GameSync.Party;
using Coop.Mod.GameSync.Roster;
using Coop.Mod.Patch.World;
using Coop.Mod.Persistence.Party;
using NLog;
using RailgunNet.Logic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Mod.GameSync
{
    public class Initializer
    {

        /// <summary>
        ///     Called after a game has been fully loaded in order to setup the game sync.
        /// </summary>
        public static void SetupSyncAfterLoad()
        {
            List<object> Objects = new List<object>();
            RegisterIfNotRegistered(Objects,Campaign.Current);
            Type type = typeof(Campaign);
            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;
            MemberInfo[] members = type.GetFields(bindingFlags).Cast<MemberInfo>()
                .Concat(type.GetProperties(bindingFlags)).ToArray();
            foreach (MemberInfo member in members)
            {
                object obj = null;
                Type MemberType = null;
                if (typeof(FieldInfo).IsAssignableFrom(member.GetType()))
                {
                    FieldInfo fieldInfo = (FieldInfo)member;
                    obj = fieldInfo.GetValue(Campaign.Current);
                    MemberType = fieldInfo.FieldType;
                }
                else if (typeof(PropertyInfo).IsAssignableFrom(member.GetType()))
                {
                    PropertyInfo propertyInfo = (PropertyInfo)member;
                    obj = propertyInfo.GetValue(Campaign.Current);
                    MemberType = propertyInfo.PropertyType;
                }
                if (obj is null && MemberType is null)
                    continue;
                if (typeof(IEnumerable).IsAssignableFrom(MemberType))
                {
                    foreach (var o in (IEnumerable)obj)
                    {
                        RegisterIfNotRegistered(Objects,o);
                    }
                }
                else
                {
                    RegisterIfNotRegistered(Objects, obj);
                }
            }

            if (Coop.IsServer)
            {
                CoopServer.Instance.Persistence.MobilePartyEntityManager.OnBeforePartyScopeEnter += OnBeforePartyScopeEnter;
            }
        }

        private static void RegisterIfNotRegistered(List<object> Objects,object obj)
        {
            if (!Objects.Contains(obj))
            {
                if (CoopFramework.CoopFramework.TryRegister(obj))
                {
                    Objects.Add(obj);
                }
            }
        }

        /// <summary>
        ///     Called just before a mobile party enters the scope of a client.
        /// </summary>
        /// <param name="controller">The client whose scope the party enters</param>
        /// <param name="entity">The entity of the party entering the scope</param>
        private static void OnBeforePartyScopeEnter(RailController controller, MobilePartyEntityServer entity)
        {
            MobileParty party = entity.Instance;
            if(party == null)
            {
                Logger.Error($"{entity} has no valid MobileParty instance. Invalid state, sync not possible.");
                return;
            }

            Logger.Trace($"{entity} entered scope of {controller}");

            // Roster might be out of date. We could keep track of what we last sent the client and only send an update if
            // it is actually outdated. But as of right now, this does seems like a lot of effort for little benefit. Easier
            // to just always send everything.
            TroopRosterSync.BroadcastTroopRosterChange(party, party.MemberRoster);
            TroopRosterSync.BroadcastTroopRosterChange(party, party.PrisonRoster);
        }

        private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();
    }
}
