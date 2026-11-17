using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace Coop.CrashReporting
{
    internal static class CrashDiagnostics
    {
        private const int MaximumPhaseCount = 8;
        private static readonly object SyncRoot = new object();
        private static readonly Queue<string> Phases = new Queue<string>(MaximumPhaseCount);

        private static volatile CrashInformationCollector.CrashInformation cachedInformation =
            CreateInformation("unknown", "unknown", "boot");
        private static string build = "unknown";
        private static string role = "unknown";
        private static string phase = "boot";

        internal static void Initialize(string buildValue, string roleValue)
        {
            lock (SyncRoot)
            {
                build = buildValue;
                role = roleValue;
                Rebuild();
            }
        }

        internal static void SetPhase(string value)
        {
            lock (SyncRoot)
            {
                phase = value;
                if (Phases.Count == MaximumPhaseCount)
                    Phases.Dequeue();

                Phases.Enqueue(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:HH:mm:ss.fff} {1}",
                    DateTime.UtcNow,
                    value));
                Rebuild();
            }
        }

        [CrashInformationCollector.CrashInformationProvider]
        private static CrashInformationCollector.CrashInformation GetCrashInformation()
        {
            return cachedInformation;
        }

        private static void Rebuild()
        {
            cachedInformation = CreateInformation(build, role, phase);
        }

        private static CrashInformationCollector.CrashInformation CreateInformation(
            string buildValue,
            string roleValue,
            string phaseValue)
        {
            var values = new List<(string, string)>
            {
                ("Build", buildValue),
                ("Role", roleValue),
                ("Phase", phaseValue),
            };

            var number = 1;
            foreach (string previousPhase in Phases)
            {
                values.Add((
                    "Phase" + number.ToString("00", CultureInfo.InvariantCulture),
                    previousPhase));
                number++;
            }

            return new CrashInformationCollector.CrashInformation(
                "BannerlordCoop",
                new MBReadOnlyList<(string, string)>(values));
        }
    }
}
