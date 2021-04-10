using System;
using System.Reflection;
using JetBrains.Annotations;

namespace Sync.Patch
{
    public class DestructorPatch<TPatch> : MethodPatch<TPatch>
    {
        public DestructorPatch([NotNull] Type declaringClass) : base(declaringClass)
        {
        }

        /// <summary>
        ///     Creates a postfix for the destructors of the type.
        /// </summary>
        /// <returns></returns>
        public DestructorPatch<TPatch> Prefix()
        {
            Intercept(
                "Finalize",
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly);
            return this;
        }
    }
}