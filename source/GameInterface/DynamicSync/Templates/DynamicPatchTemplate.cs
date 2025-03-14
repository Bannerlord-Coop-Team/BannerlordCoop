using GameInterface.Utils;
using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
@UsingDeclarations@


namespace DynamicSync
{
    [HarmonyPatch]
    public class @DynamicPatchClassName@ : GenericPatches<@DynamicPatchClassName@, @TargetType@>
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(@TargetType@)))
            {
                yield return method;
            }
            @TargetMethods@
        }

@Transpilers@
    }
}
