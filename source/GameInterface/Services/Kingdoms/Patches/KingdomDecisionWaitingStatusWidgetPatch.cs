using Common;
using HarmonyLib;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace GameInterface.Services.Kingdoms.Patches
{
    [HarmonyPatch(typeof(GauntletLayer), "LoadMovieAux")]
    internal static class KingdomDecisionWaitingStatusWidgetPatch
    {
        private const string DecisionTitleBrushName = "Kingdom.DecisionTitleBig.Text";
        private const string DecisionParagraphBrushName = "Kingdom.DecisionParagraph.Text";
        private const string WaitingStatusWidgetId = "CoopDecisionWaitingStatus";
        private const float WaitingStatusWidth = 540f;
        private const float WaitingStatusHeight = 185f;
        private const int WaitingStatusFontSize = 24;
        private static readonly ConditionalWeakTable<KingdomDecisionsVM, RichTextWidget> WaitingStatusWidgets = new();

        [HarmonyPostfix]
        private static void AddWaitingStatusWidget(ViewModel dataSource, IGauntletMovie __result)
        {
            if (!ModInformation.IsClient || __result == null) return;
            if (!(dataSource is KingdomManagementVM kingdomManagement)) return;
            if (kingdomManagement.Decision == null) return;

            RichTextWidget decisionTitle = __result.RootWidget.GetFirstInChildrenRecursive(widget =>
                widget is RichTextWidget richText &&
                string.Equals(richText.ReadOnlyBrush.Name, DecisionTitleBrushName, StringComparison.Ordinal)) as RichTextWidget;
            if (decisionTitle?.ParentWidget == null) return;

            RichTextWidget decisionParagraph = decisionTitle.ParentWidget.GetFirstInChildrenRecursive(widget =>
                widget is RichTextWidget richText &&
                string.Equals(richText.ReadOnlyBrush.Name, DecisionParagraphBrushName, StringComparison.Ordinal)) as RichTextWidget;
            if (decisionParagraph == null) return;

            var waitingStatus = new RichTextWidget(decisionTitle.Context)
            {
                Id = WaitingStatusWidgetId,
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                SuggestedWidth = WaitingStatusWidth,
                SuggestedHeight = WaitingStatusHeight,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                MarginRight = 25f,
                MarginBottom = 20f,
                DoNotAcceptEvents = true,
                AutoHideIfEmpty = true,
                Brush = decisionParagraph.ReadOnlyBrush,
                Text = string.Empty
            };
            waitingStatus.Brush.FontSize = WaitingStatusFontSize;
            waitingStatus.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Left;
            waitingStatus.Brush.TextVerticalAlignment = TextVerticalAlignment.Top;
            decisionTitle.ParentWidget.AddChild(waitingStatus);

            WaitingStatusWidgets.Remove(kingdomManagement.Decision);
            WaitingStatusWidgets.Add(kingdomManagement.Decision, waitingStatus);
        }

        internal static void Refresh(KingdomDecisionsVM decisions, string feedback)
        {
            if (decisions == null || !WaitingStatusWidgets.TryGetValue(decisions, out RichTextWidget waitingStatus)) return;

            waitingStatus.Text = feedback ?? string.Empty;
        }
    }
}
