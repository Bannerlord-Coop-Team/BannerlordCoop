using System;
using System.Collections.Generic;
using HarmonyLib;
using JetBrains.Annotations;
using Sync.Call;

namespace Sync.Patch
{
    /// <summary>
    ///     Patch generator for properties.
    /// </summary>
    public class PropertyPatch<TPatch>
    {
        private readonly Type m_Declaring;
        private readonly MethodPatch<TPatch> m_GetterPatch;
        private readonly MethodPatch<TPatch> m_SetterPatch;

        public PropertyPatch(
            [NotNull] Type declaringType)
        {
            m_Declaring = declaringType;
            m_SetterPatch = new MethodPatch<TPatch>(m_Declaring);
            m_GetterPatch = new MethodPatch<TPatch>(m_Declaring);
        }

        public IEnumerable<PatchedInvokable> Setters => m_SetterPatch.Methods;
        public IEnumerable<PatchedInvokable> Getters => m_GetterPatch.Methods;

        public PropertyPatch<TPatch> InterceptSetter(string sProperty)
        {
            m_SetterPatch.Intercept(
                AccessTools.PropertySetter(m_Declaring, sProperty));
            return this;
        }

        public PropertyPatch<TPatch> PostfixSetter(string sProperty)
        {
            m_SetterPatch.Postfix(
                AccessTools.PropertySetter(m_Declaring, sProperty));
            return this;
        }

        public PropertyPatch<TPatch> InterceptGetter(string sProperty)
        {
            m_GetterPatch.Intercept(
                AccessTools.PropertyGetter(m_Declaring, sProperty));
            return this;
        }

        public PropertyPatch<TPatch> PostfixGetter(string sProperty)
        {
            m_GetterPatch.Postfix(
                AccessTools.PropertyGetter(m_Declaring, sProperty));
            return this;
        }
    }
}