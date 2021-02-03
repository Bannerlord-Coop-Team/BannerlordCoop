using System;
using JetBrains.Annotations;

namespace Sync.Behaviour
{
    public class Condition
    {
        public Condition([NotNull] Func<EOriginator, object, bool> func)
        {
            m_Func = func;
        }

        public bool Evaluate(EOriginator eOrigin, object instance)
        {
            return m_Func(eOrigin, instance);
        }

        public static implicit operator Func<EOriginator, object, bool>(Condition c) => c.m_Func;
        public static explicit operator Condition(Func<EOriginator, object, bool> func) => new Condition(func);
        
        public static Condition operator &([NotNull] Condition lhs, [NotNull] Condition rhs)
        {
            return new Condition((eOrigin, instance) => lhs.m_Func(eOrigin, instance) && rhs.m_Func(eOrigin, instance));
        }

        private Func<EOriginator, object, bool> m_Func;
    }
}