using Common.Logging;
using GameInterface.AutoSync;
using GameInterface.AutoSync.Registry;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.Alleys
{
   
    public class AlleySync : IAutoSync
    {
        ILogger Logger { get; } = LogManager.GetLogger<AlleySync>();
        static int callCount = 0;
        public AlleySync(IAutoRegistryFactory registryFactory, IAutoSyncBuilder autoSyncBuilder) 
        {
            // Lifetime
            var alleyCtros = new MethodBase[] {
                AccessTools.Constructor(typeof(Alley), new Type[] { typeof(Settlement), typeof(string), typeof(TextObject) })
            };
            registryFactory.TryRegisterType<Alley>(alleyCtros, RegisterAllAllys);

            // Fields
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Alley), nameof(Alley._name)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Alley), nameof(Alley._settlement)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Alley), nameof(Alley._tag)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Alley), nameof(Alley._owner)));
        }

        void RegisterAllAllys(AutoRegistry<Alley> registry)
        {
            foreach (Settlement settlement in Campaign.Current.Settlements)
            {
                if (settlement.Town == null) continue;

                foreach (Alley alley in settlement.Alleys)
                {
                    if (registry.RegisterNewObject(alley, out var _) == false) Logger.Error($"Unable to register {alley}");
                }
            }
        }
    }
}
