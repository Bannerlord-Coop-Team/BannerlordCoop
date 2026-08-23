using GameInterface.Services.Kingdoms.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Kingdoms
{
    public class KingdomDecisionWaitingFeedback
    {
        public string Header { get; }
        public IReadOnlyList<string> Columns { get; }

        public KingdomDecisionWaitingFeedback(string header, IReadOnlyList<string> columns)
        {
            Header = header ?? string.Empty;
            Columns = columns ?? Array.Empty<string>();
        }
    }

    public interface IKingdomDecisionRoundPresentation
    {
        string FormatTitle(string baseTitle, int? remainingSeconds);
        string GetBaseTitle(string currentTitle);
        KingdomDecisionWaitingFeedback FormatWaitingFeedback(
            bool hasSubmitted,
            IReadOnlyList<KingdomDecisionRoundClanStatusData> waitingClans);
    }

    internal class KingdomDecisionRoundPresentation : IKingdomDecisionRoundPresentation
    {
        internal const int ColumnCount = 4;
        private const string CountdownPrefix = " (Voting ends in ";
        private const string CountdownSuffix = "s)";

        public string FormatTitle(string baseTitle, int? remainingSeconds)
        {
            string title = GetBaseTitle(baseTitle);
            if (!remainingSeconds.HasValue) return title;
            if (string.IsNullOrWhiteSpace(title))
            {
                return $"Voting ends in {remainingSeconds.Value}s";
            }

            if (!title.EndsWith("."))
            {
                title += ".";
            }

            return title + CountdownPrefix + remainingSeconds.Value + CountdownSuffix;
        }

        public string GetBaseTitle(string currentTitle)
        {
            if (string.IsNullOrEmpty(currentTitle)) return string.Empty;

            int suffixIndex = currentTitle.LastIndexOf(CountdownPrefix, StringComparison.Ordinal);
            if (suffixIndex < 0) return currentTitle;

            string suffix = currentTitle.Substring(suffixIndex);
            if (!suffix.EndsWith(CountdownSuffix, StringComparison.Ordinal)) return currentTitle;

            string remaining = suffix.Substring(
                CountdownPrefix.Length,
                suffix.Length - CountdownPrefix.Length - CountdownSuffix.Length);
            for (int i = 0; i < remaining.Length; i++)
            {
                if (remaining[i] < '0' || remaining[i] > '9') return currentTitle;
            }

            return currentTitle.Substring(0, suffixIndex);
        }

        public KingdomDecisionWaitingFeedback FormatWaitingFeedback(
            bool hasSubmitted,
            IReadOnlyList<KingdomDecisionRoundClanStatusData> waitingClans)
        {
            string prefix = hasSubmitted ? "Vote submitted. " : string.Empty;
            if (waitingClans == null || waitingClans.Count == 0)
            {
                return new KingdomDecisionWaitingFeedback(
                    prefix + "All votes received. Resolving...",
                    CreateEmptyColumns());
            }

            var names = new List<string>(waitingClans.Count);
            foreach (KingdomDecisionRoundClanStatusData clan in waitingClans)
            {
                if (clan == null) continue;
                names.Add(FormatWaitingClan(clan));
            }

            return new KingdomDecisionWaitingFeedback(
                prefix + "Waiting for remaining players:",
                SplitIntoColumns(names));
        }

        internal static string FormatWaitingClan(KingdomDecisionRoundClanStatusData clan)
        {
            string label = clan.PlayerNames;
            if (!string.Equals(clan.PlayerNames, clan.ClanName, StringComparison.OrdinalIgnoreCase))
            {
                label += $" ({clan.ClanName}";
                if (!clan.IsConnected) label += ", disconnected";
                return label + ")";
            }

            return clan.IsConnected ? label : label + " (disconnected)";
        }

        internal static IReadOnlyList<string> SplitIntoColumns(IReadOnlyList<string> names)
        {
            var columns = new string[ColumnCount];
            if (names == null || names.Count == 0)
            {
                return CreateEmptyColumns();
            }

            int columnSize = (names.Count + ColumnCount - 1) / ColumnCount;
            for (int column = 0; column < ColumnCount; column++)
            {
                int start = column * columnSize;
                if (start >= names.Count)
                {
                    columns[column] = string.Empty;
                    continue;
                }

                int end = Math.Min(start + columnSize, names.Count);
                var builder = new StringBuilder();
                for (int i = start; i < end; i++)
                {
                    if (builder.Length > 0) builder.Append('\n');
                    builder.Append(names[i]);
                }

                columns[column] = builder.ToString();
            }

            return columns;
        }

        private static IReadOnlyList<string> CreateEmptyColumns()
        {
            return new[] { string.Empty, string.Empty, string.Empty, string.Empty };
        }
    }
}
