#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.LiveTesting;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.ExtraWidgets;
using TaleWorlds.MountAndBlade.GauntletUI;

namespace GameInterface.Services.Armies.Commands;

public sealed class ArmyRemovalFixtureViewCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;
    private readonly IUiWidgetAdapter widgets;
    private static GauntletMovieIdentifier movie;
    private static GauntletLayer layer;
    private static bool showing;
    private static PropertyBasedTooltipVM tooltip;
    private static string partyId;
    private static string viewKind;

    public ArmyRemovalFixtureViewCommand(IObjectManager objectManager, IUiWidgetAdapter widgets)
    {
        this.objectManager = objectManager;
        this.widgets = widgets;
    }

    public string Prefix => "coop.debug.army";
    public string Name => "removal_fixture_view";
    public string Description => "Shows and inspects the native extended army or party tooltip without desktop input.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("action", "show, state or clear"),
        new ExpectedArgs("partyId", "Registered party id for show.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!ModInformation.IsClient || args.Count < 1)
            return new CoopCommandResult(false, "Run on a client with show, state or clear.", "command_failed");
        if (args[0] == "clear" && args.Count == 1)
        {
            InformationManager.HideTooltip();
            return new CoopCommandResult(true, "Native tooltip closed.");
        }
        if (args[0] == "show" && args.Count == 2)
        {
            if (!objectManager.TryGetObject<MobileParty>(args[1], out var party) || party.LeaderHero == null)
                return new CoopCommandResult(false, "The named lord party is missing.", "command_failed");
            var army = party.Army;
            bool joined = army != null && army.DoesLeaderPartyAndAttachedPartiesContain(party);
            partyId = args[1];
            viewKind = joined ? "army" : "party";
            showing = true;
            try
            {
                // Use the native hover view and inspection flag, preserving the actual replicated roster.
                InformationManager.ShowTooltip(joined ? typeof(Army) : typeof(MobileParty),
                    new object[] { joined ? (object)army : party, true, true });
            }
            finally { showing = false; }
        }
        else if (args[0] != "state" || args.Count != 1)
            return new CoopCommandResult(false, "Expected show partyId, state or clear.", "command_failed");
        if (tooltip == null || !tooltip.IsActive || !tooltip.IsExtended)
            return new CoopCommandResult(false, "The native extended tooltip is unavailable.", "command_failed");
        var frame = movie?.Movie?.RootWidget == null || layer == null ? null : widgets.Read(layer);
        var native = frame?.Widgets.Where(w => BelongsToMovie((Widget)w.Native)).Select(w => new
        {
            w.Id, w.Type, w.Text, w.Visible, w.X, w.Y, w.Width, w.Height,
            opacity = ((Widget)w.Native).AlphaFactor * ((Widget)w.Native).Context.ContextAlpha,
            complete = !w.ContentTruncated && !w.Redacted,
        }).ToArray();
        var page = layer?.UIContext?.EventManager.PageSize;
        return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            partyId, viewKind, active = tooltip.IsActive, extended = tooltip.IsExtended,
            movieName = movie?.Movie?.MovieName, layerName = layer?.Name,
            layerActive = layer != null && layer.IsActive && !layer.IsFinalized,
            complete = frame != null && !frame.Truncated && frame.ScopeComplete,
            pageWidth = page?.X, pageHeight = page?.Y, nativeWidgets = native,
            rows = tooltip.TooltipPropertyList.Select(p => new { definition = p.DefinitionLabel, value = p.ValueLabel }).ToArray(),
        }));
    }

    [HarmonyPatch(typeof(GauntletInformationView), "OnShowTooltip")]
    private static class CaptureNativeTooltip
    {
        [HarmonyPostfix]
        private static void Postfix(TooltipBaseVM ____dataSource, GauntletMovieIdentifier ____movie, GauntletLayer ____layerAsGauntletLayer)
        {
            if (!showing || !ModInformation.IsClient) return;
            tooltip = ____dataSource as PropertyBasedTooltipVM;
            movie = ____movie;
            layer = ____layerAsGauntletLayer;
            if (tooltip != null) tooltip.IsExtended = true;
        }
    }

    [HarmonyPatch(typeof(TooltipBaseVM), nameof(TooltipBaseVM.IsExtended), MethodType.Setter)]
    private static class KeepNativeTooltipExtended
    {
        [HarmonyPrefix]
        private static void Prefix(TooltipBaseVM __instance, ref bool value)
        {
            // Native OnTick otherwise collapses the selected view without a held desktop key.
            if (ModInformation.IsClient && ReferenceEquals(__instance, tooltip)) value = true;
        }
    }

    private static bool BelongsToMovie(Widget widget)
    {
        var root = movie?.Movie?.RootWidget;
        if (root == null) return false;
        for (int depth = 0; widget != null && depth < 48; depth++, widget = widget.ParentWidget)
            if (ReferenceEquals(widget, root)) return true;
        return false;
    }

    [HarmonyPatch(typeof(TooltipWidget), "UpdatePosition")]
    private static class FrameSelectedTooltip
    {
        [HarmonyPostfix]
        private static void Postfix(TooltipWidget __instance)
        {
            if (!ModInformation.IsClient || tooltip == null || __instance.Id != "TooltipWidget" || !BelongsToMovie(__instance)) return;
            var page = __instance.EventManager.PageSize;
            var size = __instance.Size;
            if (float.IsNaN(size.X + size.Y + page.X + page.Y) || float.IsInfinity(size.X + size.Y + page.X + page.Y) ||
                size.X <= 0 || size.Y <= 0 || size.X > page.X || size.Y > page.Y) return;
            // Center only the selected native tooltip using its measured bounds, never desktop input.
            __instance.ScaledPositionXOffset += ((page.X - size.X) / 2) - __instance.GlobalPosition.X;
            __instance.ScaledPositionYOffset += ((page.Y - size.Y) / 2) - __instance.GlobalPosition.Y;
        }
    }

    [HarmonyPatch(typeof(GauntletInformationView), "OnHideTooltip")]
    private static class ReleaseNativeTooltip
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            tooltip = null;
            movie = null;
            layer = null;
        }
    }
}
#endif
