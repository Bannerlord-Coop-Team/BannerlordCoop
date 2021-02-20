using JetBrains.Annotations;
using Sync.Behaviour;

namespace CoopFramework
{
    public class CoopConditions
    {
        /// <summary>
        ///     The action was emitted during the regular game loop. i.e. it is not a authoritative call from the
        ///     coop server.
        /// </summary>
        public static Condition GameLoop = IsOriginator(EOriginator.Game);

        /// <summary>
        ///     The action was emitted by the coop server and is to be treated as an authoritative action, that
        ///     means it is to be applied.
        /// </summary>
        public static Condition RemoteAuthority = IsOriginator(EOriginator.RemoteAuthority);

        public static Condition Not([NotNull] Condition condition)
        {
            return new Condition((originator, o) => !condition.Evaluate(originator, o));
        }

        #region Private

        private static Condition IsOriginator(EOriginator eOriginator)
        {
            return new Condition((eOrigin, _) => eOrigin == eOriginator);
        }

        #endregion
    }
}