using Common;
using Common.Logging;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager.Extensions;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Registry
{
    /// <summary>
    /// Auto-registered Hero registry using attribute-based registration
    /// </summary>
    [AutoRegister(typeof(Hero))]
    internal class AutoHeroRegistry : IAutoRegistry<Hero>
    {
        private static readonly ILogger Logger = LogManager.GetLogger<AutoHeroRegistry>();

        public IEnumerable<MethodBase> Constructors => new MethodBase[] {
            AccessTools.Constructor(typeof(Hero), new Type[] { typeof(string) })
        };

        public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

        public void RegisterAllObjects(IRegistry<Hero> registry)
        {
            var campaignObjectManager = Campaign.Current?.CampaignObjectManager;

            if (campaignObjectManager == null)
            {
                Logger.Error("Unable to register objects when CampaignObjectManager is null");
                return;
            }

            foreach (var hero in campaignObjectManager.GetAllHeroes())
            {
                registry.RegisterExistingObject(hero.StringId, hero);
            }
        }

        public void OnClientCreated(Hero obj, string id)
        {
            Logger.Debug("Client created Hero: {Id} - {Name}", id, obj.Name);
        }

        public void OnClientDestroyed(Hero obj, string id)
        {
            Logger.Debug("Client destroyed Hero: {Id} - {Name}", id, obj.Name);
        }

        public void OnServerCreated(Hero obj, string id)
        {
            Logger.Debug("Server created Hero: {Id} - {Name}", id, obj.Name);
        }

        public void OnServerDestroyed(Hero obj, string id)
        {
            Logger.Debug("Server destroyed Hero: {Id} - {Name}", id, obj.Name);
        }
    }
}