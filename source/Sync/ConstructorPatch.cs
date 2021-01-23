using System;
using System.Reflection;
using JetBrains.Annotations;

namespace Sync
{
    public class ConstructorPatch : MethodPatch
    {
        public ConstructorPatch([NotNull] Type declaringClass) : base(declaringClass)
        {
        }

        public ConstructorPatch PostfixAll()
        {
            foreach (ConstructorInfo info in m_Declaring.GetConstructors())
            {
                Postfix(info);
            }

            return this;
        }
    }
}