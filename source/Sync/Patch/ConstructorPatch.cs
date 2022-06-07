using System;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace Sync.Patch
{
    /// <summary>
    ///     Creates a patch builder for constructors of a type.
    /// </summary>
    public class ConstructorPatch<TPatch> : MethodPatch<TPatch>
    {
        public ConstructorPatch([NotNull] Type declaringClass) : base(declaringClass)
        {
        }

        /// <summary>
        ///     Creates a postfix for all constructors of the type.
        /// </summary>
        /// <returns></returns>
        public ConstructorPatch<TPatch> PostfixAll()
        {
            foreach (var info in m_Declaring.GetConstructors(BindingFlags.Public | BindingFlags.Static |
                                                       BindingFlags.Instance | BindingFlags.NonPublic |
                                                       BindingFlags.DeclaredOnly))
            {
                Postfix(info);
            }

            return this;
        }
    }
}