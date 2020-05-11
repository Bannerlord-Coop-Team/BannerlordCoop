using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Coop.Reflection
{
    public class Util
    {
        public static MethodBase GetOriginalMethod(HarmonyMethod attr)
        {
            if (attr.declaringType == null) return null;

            if (attr.methodType == null)
            {
                attr.methodType = MethodType.Normal;
            }

            switch (attr.methodType)
            {
                case MethodType.Normal:
                    if (attr.methodName == null)
                    {
                        return null;
                    }

                    return AccessTools.DeclaredMethod(
                        attr.declaringType,
                        attr.methodName,
                        attr.argumentTypes);

                case MethodType.Getter:
                    if (attr.methodName == null)
                    {
                        return null;
                    }

                    return AccessTools.DeclaredProperty(attr.declaringType, attr.methodName)
                                      .GetGetMethod(true);

                case MethodType.Setter:
                    if (attr.methodName == null)
                    {
                        return null;
                    }

                    return AccessTools.DeclaredProperty(attr.declaringType, attr.methodName)
                                      .GetSetMethod(true);

                case MethodType.Constructor:
                    return AccessTools.DeclaredConstructor(attr.declaringType, attr.argumentTypes);

                case MethodType.StaticConstructor:
                    return AccessTools.GetDeclaredConstructors(attr.declaringType)
                                      .FirstOrDefault(c => c.IsStatic);
            }

            return null;
        }
    }
}
