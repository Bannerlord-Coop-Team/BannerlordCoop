using Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.GauntletUI.Layout;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace GameInterface.Services.Kingdoms.Patches
{
    [HarmonyPatch(typeof(GauntletLayer), "LoadMovieAux")]
    internal static class KingdomDecisionWaitingStatusWidgetPatch
    {
        private const string DecisionTitleBrushName = "Kingdom.DecisionTitleBig.Text";
        private const string DecisionParagraphBrushName = "Kingdom.DecisionParagraph.Text";
        private const string DecisionSurfaceId = "ParentContainer";
        private const string WaitingStatusWidgetId = "CoopDecisionWaitingStatus";
        private const float WaitingStatusHeight = 168f;
        private const float WaitingStatusSideWidth = 520f;
        private const float WaitingStatusColumnBottomMargin = 55f;
        private const int WaitingStatusHeaderFontSize = 20;
        private const int WaitingStatusColumnFontSize = 18;
        private const int DecisionTitleDefaultFontSize = 38;
        private const int DecisionTitleMinimumFontSize = 18;
        private const int DecisionTitleWidthBudget = 2250;
        private static readonly ConditionalWeakTable<KingdomDecisionsVM, IGauntletMovie> DecisionMovies = new();
        private static readonly ConditionalWeakTable<KingdomDecisionsVM, WaitingStatusOverlay> WaitingStatusOverlays = new();
        private static WeakReference<KingdomManagementVM> LatestKingdomManagement;
        private static WeakReference<IGauntletMovie> LatestKingdomMovie;

        [HarmonyPostfix]
        private static void AddWaitingStatusWidget(ViewModel dataSource, IGauntletMovie __result)
        {
            if (!ModInformation.IsClient || __result == null) return;
            if (!(dataSource is KingdomManagementVM kingdomManagement)) return;

            LatestKingdomManagement = new WeakReference<KingdomManagementVM>(kingdomManagement);
            LatestKingdomMovie = new WeakReference<IGauntletMovie>(__result);
            if (kingdomManagement.Decision != null)
            {
                RememberMovie(kingdomManagement.Decision, __result);
            }

            TryAttach(kingdomManagement.Decision, __result);
        }

        internal static void EnsureAttached(KingdomDecisionsVM decisions)
        {
            if (decisions == null) return;
            if (WaitingStatusOverlays.TryGetValue(decisions, out WaitingStatusOverlay overlay) &&
                overlay.Root?.ParentWidget != null)
            {
                return;
            }

            if (!TryGetMovie(decisions, out IGauntletMovie movie)) return;

            TryAttach(decisions, movie);
        }

        internal static void Refresh(
            KingdomDecisionsVM decisions,
            string header,
            IReadOnlyList<string> columns)
        {
            EnsureAttached(decisions);
            if (decisions == null || !WaitingStatusOverlays.TryGetValue(decisions, out WaitingStatusOverlay overlay)) return;

            overlay.Header.Text = header ?? string.Empty;
            IReadOnlyList<string> columnTexts = columns ?? Array.Empty<string>();
            bool hasContent = !string.IsNullOrWhiteSpace(header);
            for (int i = 0; i < overlay.Columns.Length; i++)
            {
                overlay.Columns[i].Text = i < columnTexts.Count ? columnTexts[i] ?? string.Empty : string.Empty;
                hasContent |= !string.IsNullOrWhiteSpace(overlay.Columns[i].Text);
            }

            overlay.Root.IsVisible = hasContent;
            overlay.DecisionTitle.Brush.FontSize = hasContent
                ? GetDecisionTitleFontSize(overlay.DecisionTitle.Text)
                : DecisionTitleDefaultFontSize;
        }

        private static bool TryGetMovie(KingdomDecisionsVM decisions, out IGauntletMovie movie)
        {
            if (DecisionMovies.TryGetValue(decisions, out movie) && movie != null)
            {
                return true;
            }

            if (LatestKingdomManagement != null &&
                LatestKingdomMovie != null &&
                LatestKingdomManagement.TryGetTarget(out KingdomManagementVM management) &&
                management.Decision == decisions &&
                LatestKingdomMovie.TryGetTarget(out movie) &&
                movie != null)
            {
                RememberMovie(decisions, movie);
                return true;
            }

            movie = null;
            return false;
        }

        private static void RememberMovie(KingdomDecisionsVM decisions, IGauntletMovie movie)
        {
            DecisionMovies.Remove(decisions);
            DecisionMovies.Add(decisions, movie);
        }

        private static void TryAttach(KingdomDecisionsVM decisions, IGauntletMovie movie)
        {
            if (decisions == null || movie?.RootWidget == null) return;

            RichTextWidget decisionTitle = movie.RootWidget.GetFirstInChildrenRecursive(widget =>
                widget is RichTextWidget richText &&
                string.Equals(richText.ReadOnlyBrush.Name, DecisionTitleBrushName, StringComparison.Ordinal) &&
                FindDecisionSurface(richText) != null) as RichTextWidget;
            if (decisionTitle?.ParentWidget == null) return;

            RichTextWidget decisionParagraph = decisionTitle.ParentWidget.GetFirstInChildrenRecursive(widget =>
                widget is RichTextWidget richText &&
                string.Equals(richText.ReadOnlyBrush.Name, DecisionParagraphBrushName, StringComparison.Ordinal)) as RichTextWidget;
            if (decisionParagraph == null) return;

            Widget decisionPanel = decisionTitle.ParentWidget;

            if (WaitingStatusOverlays.TryGetValue(decisions, out WaitingStatusOverlay existing) &&
                existing.Root != null)
            {
                if (existing.Root.ParentWidget != decisionPanel)
                {
                    existing.Root.ParentWidget?.RemoveChild(existing.Root);
                    decisionPanel.AddChild(existing.Root);
                }

                return;
            }

            WaitingStatusOverlay overlay = CreateOverlay(decisionTitle, decisionParagraph);
            decisionPanel.AddChild(overlay.Root);
            WaitingStatusOverlays.Remove(decisions);
            WaitingStatusOverlays.Add(decisions, overlay);
        }

        private static Widget FindDecisionSurface(Widget decisionTitle)
        {
            for (Widget current = decisionTitle?.ParentWidget; current != null; current = current.ParentWidget)
            {
                if (string.Equals(current.Id, DecisionSurfaceId, StringComparison.Ordinal)) return current;
            }

            return null;
        }

        private static WaitingStatusOverlay CreateOverlay(RichTextWidget decisionTitle, RichTextWidget decisionParagraph)
        {
            var root = new Widget(decisionTitle.Context)
            {
                Id = WaitingStatusWidgetId,
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.Fixed,
                SuggestedHeight = WaitingStatusHeight,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                MarginLeft = 20f,
                MarginRight = 20f,
                MarginBottom = 16f,
                DoNotAcceptEvents = true,
                IsVisible = false
            };

            var header = new RichTextWidget(decisionTitle.Context)
            {
                WidthSizePolicy = SizePolicy.Fixed,
                SuggestedWidth = WaitingStatusSideWidth,
                HeightSizePolicy = SizePolicy.Fixed,
                SuggestedHeight = 28f,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                DoNotAcceptEvents = true,
                AutoHideIfEmpty = true,
                Brush = decisionParagraph.ReadOnlyBrush,
                Text = string.Empty
            };
            header.Brush.FontSize = WaitingStatusHeaderFontSize;
            header.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Left;
            header.Brush.TextVerticalAlignment = TextVerticalAlignment.Top;

            var columns = new RichTextWidget[KingdomDecisionRoundPresentation.ColumnCount];
            ListPanel leftColumns = CreateColumnPanel(
                decisionTitle,
                decisionParagraph,
                HorizontalAlignment.Left,
                columns,
                0);
            ListPanel rightColumns = CreateColumnPanel(
                decisionTitle,
                decisionParagraph,
                HorizontalAlignment.Right,
                columns,
                KingdomDecisionRoundPresentation.ColumnCount / 2);

            root.AddChild(leftColumns);
            root.AddChild(rightColumns);
            root.AddChild(header);
            return new WaitingStatusOverlay(root, decisionTitle, header, columns);
        }

        private static ListPanel CreateColumnPanel(
            RichTextWidget decisionTitle,
            RichTextWidget decisionParagraph,
            HorizontalAlignment horizontalAlignment,
            RichTextWidget[] columns,
            int startIndex)
        {
            var columnPanel = new ListPanel(decisionTitle.Context)
            {
                WidthSizePolicy = SizePolicy.Fixed,
                SuggestedWidth = WaitingStatusSideWidth,
                HeightSizePolicy = SizePolicy.Fixed,
                SuggestedHeight = WaitingStatusHeight - 91f,
                HorizontalAlignment = horizontalAlignment,
                VerticalAlignment = VerticalAlignment.Bottom,
                MarginBottom = WaitingStatusColumnBottomMargin,
                DoNotAcceptEvents = true
            };
            columnPanel.StackLayout.LayoutMethod = LayoutMethod.HorizontalLeftToRight;

            int endIndex = startIndex + (KingdomDecisionRoundPresentation.ColumnCount / 2);
            for (int i = startIndex; i < endIndex; i++)
            {
                var column = new RichTextWidget(decisionTitle.Context)
                {
                    WidthSizePolicy = SizePolicy.StretchToParent,
                    HeightSizePolicy = SizePolicy.StretchToParent,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    MarginLeft = 8f,
                    MarginRight = 8f,
                    DoNotAcceptEvents = true,
                    Brush = decisionParagraph.ReadOnlyBrush,
                    Text = string.Empty
                };
                column.Brush.FontSize = WaitingStatusColumnFontSize;
                column.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Left;
                column.Brush.TextVerticalAlignment = TextVerticalAlignment.Top;
                columnPanel.AddChild(column);
                columns[i] = column;
            }

            return columnPanel;
        }

        private static int GetDecisionTitleFontSize(string title)
        {
            if (string.IsNullOrEmpty(title)) return DecisionTitleDefaultFontSize;

            return Math.Max(
                DecisionTitleMinimumFontSize,
                Math.Min(DecisionTitleDefaultFontSize, DecisionTitleWidthBudget / title.Length));
        }

        private sealed class WaitingStatusOverlay
        {
            public Widget Root { get; }
            public RichTextWidget DecisionTitle { get; }
            public RichTextWidget Header { get; }
            public RichTextWidget[] Columns { get; }

            public WaitingStatusOverlay(
                Widget root,
                RichTextWidget decisionTitle,
                RichTextWidget header,
                RichTextWidget[] columns)
            {
                Root = root;
                DecisionTitle = decisionTitle;
                Header = header;
                Columns = columns;
            }
        }
    }
}
