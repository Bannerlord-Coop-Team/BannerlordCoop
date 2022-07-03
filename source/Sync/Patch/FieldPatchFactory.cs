using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Sync.Call;
using Sync.Value;

namespace Sync.Patch
{
    public class FieldPatcher
    {
        #region Static
        static void GenericIntercept<T>(T newValue, object instance, int id)
        {
            FieldPatch patch = FieldPatch.FieldPatches[id];
            lock(patch.Field)
            {
                if (patch.ChangeAllowed() == Behaviour.EFieldChangeAction.Allow)
                {
                    patch.Field.SetValue(instance, newValue);
                }
            }
        }

        private static MethodInfo _hookMethod = typeof(FieldPatcher).GetMethod(nameof(FieldPatcher.GenericIntercept), BindingFlags.NonPublic | BindingFlags.Static);

        private static MethodInfo _transpiler = typeof(FieldPatcher).GetMethod(nameof(FieldPatcher.Transpiler), BindingFlags.NonPublic | BindingFlags.Static);

        private static Dictionary<MethodBase, List<FieldPatch>> _fields = new Dictionary<MethodBase, List<FieldPatch>>();

        private static readonly Dictionary<MethodBase, MethodInfo> Transpilers =
            new Dictionary<MethodBase, MethodInfo>();
        #endregion

        public static void AddTranspiler(PatchedInvokable access)
        {
            lock (Patcher.HarmonyLock)
            {
                var patch = new HarmonyMethod(_transpiler)
                {
                    priority = SyncPriority.MethodPatchGenerated,
#if DEBUG
                    debug = true
#endif
                };
                MethodInfo patchedMethod = Patcher.HarmonyInstance.Patch(access.Original, transpiler: patch);

                if (Transpilers.ContainsKey(access.Original))
                {
                    Transpilers[access.Original] = patchedMethod;
                }
                else
                {
                    Transpilers.Add(access.Original, patchedMethod);
                }
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr, MethodBase original)
        {
            List<CodeInstruction> lInstrs = instr.ToList();
            foreach(FieldPatch field in _fields[original])
            {
                ReplaceNativeAssign(lInstrs, field);
            }
            
            return lInstrs;
        }

        public static void GeneratePatch(
            FieldPatch field,
            MethodInfo caller)
        {
            if (_fields.ContainsKey(caller) &&
                !_fields[caller].Contains(field))
            {
                _fields[caller].Add(field);
            }
            else
            {
                _fields.Add(caller, new List<FieldPatch> { field });
            }
        }

        public static void UnpatchAll()
        {
            lock (Patcher.HarmonyLock)
            {
                Patcher.HarmonyInstance.UnpatchAll();
                Transpilers.Clear();
            }
        }

        static void ReplaceNativeAssign(List<CodeInstruction> codeInstructions, FieldPatch fieldPatch)
        {
            MethodInfo getTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle");

            MethodInfo GetField = typeof(Type).GetMethod(
                "GetField", 
                BindingFlags.Instance | BindingFlags.Public, 
                null, 
                new Type[]
                {
                    typeof(string),
                    typeof(BindingFlags),
                }, 
                null);

            FieldInfo field = fieldPatch.Field;

            var isPublic = field.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic;
            var isStatic = field.IsStatic ? BindingFlags.Static : BindingFlags.Instance;

            CodeInstruction[] assignmentInterceptBlock =
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldc_I4, fieldPatch.Id),
                new CodeInstruction(OpCodes.Call, _hookMethod.MakeGenericMethod(field.FieldType)),

            };

            CodeInstruction[] assignmentInterceptBlockStatic =
            {
                new CodeInstruction(OpCodes.Ldnull),
                new CodeInstruction(OpCodes.Ldc_I4, fieldPatch.Id),
                new CodeInstruction(OpCodes.Call, _hookMethod.MakeGenericMethod(field.FieldType)),
            };

            Stack<int> classRefernceStack = new Stack<int>();

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                var instr = codeInstructions[i];
                if (instr.opcode == OpCodes.Ldarg_0)
                {
                    classRefernceStack.Push(i);
                }
                else if(instr.opcode == OpCodes.Ldfld)
                {
                    classRefernceStack.Pop();
                }
                else if(instr.opcode == OpCodes.Ldflda)
                {
                    if (instr.operand as FieldInfo == field)
                    {
                        // TODO intercept by ref                       

                    }
                    else
                    {
                        classRefernceStack.Pop();
                    }
                }
                else if(instr.opcode == OpCodes.Stfld)
                {
                    if (instr.operand as FieldInfo == field)
                    {
                        codeInstructions.RemoveAt(i);
                        codeInstructions.InsertRange(i, assignmentInterceptBlock);
                        codeInstructions.RemoveAt(classRefernceStack.Pop());
                        i += assignmentInterceptBlock.Length - 2;
                    }
                    else
                    {
                        classRefernceStack.Pop();
                    }
                }
                else if (instr.opcode == OpCodes.Stsfld)
                {
                    if (instr.operand as FieldInfo == field)
                    {
                        codeInstructions.RemoveAt(i);
                        codeInstructions.InsertRange(i, assignmentInterceptBlockStatic);
                        i += assignmentInterceptBlock.Length - 1;
                    }
                }
            }
        }
    }
}
