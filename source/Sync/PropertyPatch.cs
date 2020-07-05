using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;

namespace Sync
{
    /// <summary>
    ///     Patch generator for properties.
    /// </summary>
    public class PropertyPatch
    {
        private readonly EPatchBehaviour m_Behaviour;
        private readonly Type m_Declaring;
        private readonly MethodPatch m_GetterPatch;
        private readonly MethodPatch m_SetterPatch;

        public PropertyPatch(
            [NotNull] Type declaringType,
            EPatchBehaviour eBehaviour = EPatchBehaviour.AlwaysCallOriginal)
        {
            m_Declaring = declaringType;
            m_Behaviour = eBehaviour;
            m_SetterPatch = new MethodPatch(m_Declaring);
            m_GetterPatch = new MethodPatch(m_Declaring);
        }

        public IEnumerable<MethodAccess> Setters => m_SetterPatch.Methods;
        public IEnumerable<MethodAccess> Getters => m_GetterPatch.Methods;

        public PropertyPatch InterceptSetter([NotNull] PropertyInfo property)
        {
            m_SetterPatch.Intercept(
                AccessTools.PropertySetter(m_Declaring, property.Name),
                m_Behaviour);
            return this;
        }

        public PropertyPatch InterceptSetter(string sProperty)
        {
            m_SetterPatch.Intercept(
                AccessTools.PropertySetter(m_Declaring, sProperty),
                m_Behaviour);
            return this;
        }

        public PropertyPatch InterceptGetter([NotNull] PropertyInfo property)
        {
            m_SetterPatch.Intercept(
                AccessTools.PropertyGetter(m_Declaring, property.Name),
                m_Behaviour);
            return this;
        }

        public PropertyPatch InterceptGetter(string sProperty)
        {
            m_SetterPatch.Intercept(
                AccessTools.PropertyGetter(m_Declaring, sProperty),
                m_Behaviour);
            return this;
        }
    }
}
