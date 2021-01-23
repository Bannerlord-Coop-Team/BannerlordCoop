using System;
using System.Reflection;
using JetBrains.Annotations;

namespace Sync
{
    /// <summary>
    ///     Creates a patch builder for constructors of a type.
    /// </summary>
    public class ConstructorPatch : MethodPatch
    {
        public ConstructorPatch([NotNull] Type declaringClass) : base(declaringClass)
        {
        }

        /// <summary>
        ///     Creates a postfix for all constructors of the type.
        /// </summary>
        /// <returns></returns>
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