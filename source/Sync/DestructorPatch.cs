using System;
using System.Reflection;
using JetBrains.Annotations;

namespace Sync
{
    public class DestructorPatch : MethodPatch
    {
        public DestructorPatch([NotNull] Type declaringClass) : base(declaringClass)
        {
        }

        /// <summary>
        ///     Creates a postfix for all constructors of the type.
        /// </summary>
        /// <returns></returns>
        public DestructorPatch Prefix()
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