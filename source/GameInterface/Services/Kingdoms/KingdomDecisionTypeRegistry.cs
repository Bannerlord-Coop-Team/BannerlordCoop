using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms
{
    /// <summary>
    /// Kingdom decision types compat mods sync on top of the vanilla ones, so players vote on them.
    /// <para>
    /// Call <see cref="Register{TDecision}"/> while your module loads (OnSubModuleLoad), on the server and on every
    /// client. The registry lives for the whole game process because Coop builds a new container every session.
    /// The server refuses a join when the player's registered types differ from its own, so every connected player
    /// can rebuild every decision the server sends and the decision lists stay aligned. Unregistered mod types are
    /// decided by the server AI instead of player votes.
    /// </para>
    /// </summary>
    public static class KingdomDecisionTypeRegistry
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<Type, Func<KingdomDecision, byte[]>> Writers = new Dictionary<Type, Func<KingdomDecision, byte[]>>();
        private static readonly Dictionary<string, Func<byte[], KingdomDecision>> Readers = new Dictionary<string, Func<byte[], KingdomDecision>>(StringComparer.Ordinal);

        /// <summary>
        /// Syncs <typeparamref name="TDecision"/>. Coop sends the shared decision fields (proposer, kingdom, trigger
        /// time, flags); <paramref name="write"/> and <paramref name="read"/> handle the rest of the decision.
        /// </summary>
        public static void Register<TDecision>(Func<TDecision, byte[]> write, Func<byte[], TDecision> read) where TDecision : KingdomDecision
        {
            if (write == null) throw new ArgumentNullException(nameof(write));
            if (read == null) throw new ArgumentNullException(nameof(read));

            lock (Gate)
            {
                Writers[typeof(TDecision)] = decision => write((TDecision)decision);
                Readers[typeof(TDecision).FullName] = payload => read(payload);
            }
        }

        /// <summary>Full names of the registered types, sorted, as reported when joining.</summary>
        public static string[] GetTypeNames()
        {
            lock (Gate)
            {
                return Readers.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            }
        }

        /// <summary>
        /// True when a joining player registered exactly the same types as this server; otherwise
        /// <paramref name="reason"/> names every difference.
        /// </summary>
        public static bool Matches(IEnumerable<string> playerTypeNames, out string reason)
        {
            var server = new HashSet<string>(GetTypeNames(), StringComparer.Ordinal);
            var player = new HashSet<string>(playerTypeNames ?? Array.Empty<string>(), StringComparer.Ordinal);

            var errors = server.Where(name => !player.Contains(name))
                .Select(name => $"The server syncs kingdom decision type '{name}', but this client does not. Install the same compat mod version as the server.")
                .Concat(player.Where(name => !server.Contains(name))
                    .Select(name => $"This client syncs kingdom decision type '{name}', but the server does not. Use the same compat mods as the server."))
                .OrderBy(error => error, StringComparer.Ordinal)
                .ToList();

            reason = errors.Count == 0 ? null : string.Join(Environment.NewLine, errors);
            return errors.Count == 0;
        }

        internal static bool TryGetWriter(Type type, out Func<KingdomDecision, byte[]> write)
        {
            lock (Gate)
            {
                return Writers.TryGetValue(type, out write);
            }
        }

        internal static bool TryGetReader(string typeName, out Func<byte[], KingdomDecision> read)
        {
            lock (Gate)
            {
                read = null;
                return typeName != null && Readers.TryGetValue(typeName, out read);
            }
        }

        internal static void Unregister<TDecision>() where TDecision : KingdomDecision
        {
            lock (Gate)
            {
                Writers.Remove(typeof(TDecision));
                Readers.Remove(typeof(TDecision).FullName);
            }
        }
    }
}
