using Common;
using Common.Util;
using GameInterface.AutoSync;
using GameInterface.AutoSync.Registry;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Localization;

namespace GameInterface.Services.Settlements;
internal class SettlementSync : IAutoSync
{
    public SettlementSync(IAutoRegistryFactory registryFactory)
    {
        var ctors = new MethodBase[] {
            AccessTools.Constructor(typeof(Settlement), new Type[] {
                typeof(TextObject),
                typeof(LocationComplex),
                typeof(PartyTemplateObject)}),
        };
        registryFactory.TryRegisterType<Settlement>(ctors, RegisterAll, OnClientCreated);
    }

    private void RegisterAll(AutoRegistry<Settlement> registry)
    {
        foreach (var settlement in Campaign.Current?.CampaignObjectManager?.Settlements)
        {
            registry.RegisterNewObject(settlement, out _);
        }
    }

    private void OnClientCreated(string id, Settlement settlement)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                settlement.InitSettlement();
            }
        });
    }
}
