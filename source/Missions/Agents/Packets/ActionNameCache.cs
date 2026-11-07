using Missions.Agents.Handlers;
using System;
using System.Collections.Concurrent;

namespace Missions.Agents.Packets
{
    /// <summary>
    /// Remembers what the native animation tables answered for an action code (name, siege usage and order gesture
    /// classification, resolved index) so the per-frame action poll skips the reflective native call after the first
    /// sighting. Codes are fixed once the action sets load, and a null name is never stored.
    /// </summary>
    internal sealed class ActionNameCache
    {
        /// <summary>What is known about one action code, computed once from its name.</summary>
        internal sealed class Entry
        {
            internal Entry(string name)
            {
                Name = name;
                IsRangedSiegeUsage = AgentActionHandler.IsRangedSiegeUsage(name);
                IsOrderGesture = AgentActionHandler.IsOrderGesture(name);
            }

            internal string Name { get; }
            internal bool IsRangedSiegeUsage { get; }
            internal bool IsOrderGesture { get; }

            // Negative until resolved, only a successful resolve is kept.
            internal int ResolvedIndex = -1;
        }

        // Only the name source writes names. Entries are keyed by whatever name the caller just got, which a patched
        // name source can change, so an entry is reused only while it holds the same string instance.
        private readonly ConcurrentDictionary<int, string> names = new ConcurrentDictionary<int, string>();
        private readonly ConcurrentDictionary<int, Entry> entries = new ConcurrentDictionary<int, Entry>();
        private readonly Func<int, string> nameSource;
        private readonly Func<string, int> indexSource;

        internal ActionNameCache(Func<int, string> nameSource, Func<string, int> indexSource)
        {
            this.nameSource = nameSource;
            this.indexSource = indexSource;
        }

        internal int Count => names.Count;

        internal string GetName(int actionCode)
        {
            if (names.TryGetValue(actionCode, out string cached)) return cached;

            string name = nameSource(actionCode);
            if (name != null) names[actionCode] = name;
            return name;
        }

        /// <summary>Entry for a code and the name the name source just gave for it, or null when there is no name.</summary>
        internal Entry Describe(int actionCode, string name)
        {
            if (name == null) return null;
            if (entries.TryGetValue(actionCode, out Entry cached) && ReferenceEquals(cached.Name, name))
                return cached;

            var entry = new Entry(name);
            entries[actionCode] = entry;
            return entry;
        }

        /// <summary>Index the name resolves to, asking the index source only until it answers with a valid one.</summary>
        internal int ResolveIndex(int actionCode, string name)
        {
            Entry entry = Describe(actionCode, name);
            int index = entry.ResolvedIndex;
            if (index >= 0) return index;

            index = indexSource(name);
            if (index >= 0) entry.ResolvedIndex = index;
            return index;
        }
    }
}
