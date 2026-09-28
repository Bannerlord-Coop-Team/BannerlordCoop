using Common.Util;
using GameInterface.Services.Settlements.Patches;
using GameInterface.Tests.Services.SiegeEvents;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using Xunit;

namespace GameInterface.Tests.Services.Settlements;

[Collection(nameof(CampaignCurrentCollection))]
public class ArenaAccessTests
{
    private static bool isDay;

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void ArenaAccess_AllowsDayAndNightButPreservesDisguiseRestriction(bool daytime, bool disguised)
    {
        var previousCampaign = Campaign.Current;
        var harmony = new Harmony($"arena-access-test-{Guid.NewGuid()}");
        var campaign = ObjectHelper.SkipConstructor<Campaign>();
        campaign.IsMainHeroDisguised = disguised;
        var model = new DefaultSettlementAccessModel();

        try
        {
            Campaign.Current = campaign;
            isDay = daytime;
            harmony.Patch(AccessTools.PropertyGetter(typeof(Campaign), nameof(Campaign.IsDay)),
                prefix: new HarmonyMethod(typeof(ArenaAccessTests), nameof(GetIsDay)));

            bool originalAllowed = model.CanMainHeroAccessLocation(null, "arena", out _, out var originalReason);
            Assert.Equal(daytime && !disguised, originalAllowed);

            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(DefaultSettlementAccessModelPatches)).Patch());
            bool allowed = model.CanMainHeroAccessLocation(null, "arena", out var disabled, out var reason);

            Assert.Equal(!disguised, allowed);
            Assert.Equal(disguised, disabled);
            if (disguised)
                Assert.Equal(originalReason.ToString(), reason.ToString());
            else
                Assert.Null(reason);

            Assert.True(model.CanMainHeroDoSettlementAction(null,
                SettlementAccessModel.SettlementAction.WalkAroundTheArena, out disabled, out reason));
            Assert.False(disabled);
            Assert.Null(reason);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            Campaign.Current = previousCampaign;
        }
    }

    private static bool GetIsDay(ref bool __result)
    {
        __result = isDay;
        return false;
    }
}
