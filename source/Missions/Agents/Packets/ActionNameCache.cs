using Missions.Agents.Handlers;
using System;
using System.Collections.Concurrent;

namespace Missions.Agents.Packets
{
    /// <summary>
    /// Remembers what the native animation tables answered for an action code: its name, whether it is a ranged
    /// siege use or a main-agent order gesture, and the index the name resolves to. Codes are fixed once the
    /// action sets load, so a hit skips the reflective native call on the per-frame action poll.
    /// </summary>
    /// <remarks>
    /// A null name is never stored, so a missing animation resolver (E2E runtime) keeps falling through to the
    /// caller's fallback. An entry is only trusted while the name handed to <see cref="Describe"/> is the same
    /// string instance it was filled from, so a patched or changed name source can't read a stale answer.
    /// </remarks>
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

        private readonly ConcurrentDictionary<int, Entry> entries = new ConcurrentDictionary<int, Entry>();
        private readonly Func<int, string> nameSource;
        private readonly Func<string, int> indexSource;

        internal ActionNameCache(Func<int, string> nameSource, Func<string, int> indexSource)
        {
            this.nameSource = nameSource;
            this.indexSource = indexSource;
        }

        internal int Count => entries.Count;

        internal string GetName(int actionCode)
        {
            if (entries.TryGetValue(actionCode, out Entry cached)) return cached.Name;

            string name = nameSource(actionCode);
            if (name != null) entries[actionCode] = new Entry(name);
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
