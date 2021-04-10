using System;
using JetBrains.Annotations;

namespace Sync.Behaviour
{
    /// <summary>
    ///     A boolean operator that evaluates the <see cref="EOriginator"/> and an <see cref="object"/> instance.
    /// </summary>
    public class Condition
    {
        /// <summary>
        ///     Creates a new condition.
        /// </summary>
        /// <param name="func">Function to evaluate the condition.</param>
        public Condition([NotNull] Func<EOriginator, object, bool> func)
        {
            m_Func = func;
        }

        /// <summary>
        ///     Evaluates the condition for the given parameters.
        /// </summary>
        /// <param name="eOrigin"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public bool Evaluate(EOriginator eOrigin, object instance)
        {
            return m_Func(eOrigin, instance);
        }

        /// <summary>
        ///     Conversion operator to the underlying <see cref="Func{T1,T2,T3,T4,T5,T6,T7,T8,T9,TResult}"/>.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static implicit operator Func<EOriginator, object, bool>(Condition c)
        {
            return c.m_Func;
        }

        /// <summary>
        ///     Conversion operator from a <see cref="Func{T1,T2,T3,T4,T5,T6,T7,T8,T9,TResult}"/>.
        /// </summary>
        /// <param name="func"></param>
        /// <returns></returns>
        public static explicit operator Condition(Func<EOriginator, object, bool> func)
        {
            return new Condition(func);
        }

        /// <summary>
        ///     Operator to chain two conditions with an AND.
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns></returns>
        public static Condition operator &([NotNull] Condition lhs, [NotNull] Condition rhs)
        {
            return new Condition((eOrigin, instance) => lhs.m_Func(eOrigin, instance) && rhs.m_Func(eOrigin, instance));
        }
        
        #region Private
        private readonly Func<EOriginator, object, bool> m_Func;
        #endregion
    }
}