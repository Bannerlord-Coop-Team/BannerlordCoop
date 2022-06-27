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
    public class FieldPatchFactory
    {
        private static MethodInfo _transpiler = typeof(FieldPatchFactory).GetMethod("Transpiler", BindingFlags.NonPublic | BindingFlags.Static);

        private static Dictionary<MethodBase, List<FieldBase>> _fields = new Dictionary<MethodBase, List<FieldBase>>();

        private static readonly Dictionary<MethodBase, MethodInfo> Transpilers =
            new Dictionary<MethodBase, MethodInfo>();

        public static MethodInfo hookMethod { get; set; }
        public static FieldInfo tempField { get; set; }

        public static void AddTranspiler(PatchedInvokable access,
                                         MethodInfo dispatcher)
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

                Transpilers.Add(access.Original, patchedMethod);
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr, MethodBase original)
        {
            List<CodeInstruction> lInstrs = instr.ToList();
            foreach(FieldBase field in _fields[original])
            {
                ReplaceNativeAssign(lInstrs, field, hookMethod);
            }
            
            return lInstrs;
        }

        public static void GeneratePatch(
            FieldId fieldId,
            MethodInfo caller,
            MethodInfo interceptMethod)
        {
            var field = Registry.IdToField[fieldId];
            if (_fields.ContainsKey(caller) &&
                !_fields[caller].Contains(field))
            {
                _fields[caller].Add(field);
            }
            else
            {
                _fields.Add(caller, new List<FieldBase> { field });
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

        static void ReplaceNativeAssign(List<CodeInstruction> codeInstructions, FieldBase field, MethodInfo hookMethod)
        {
            // Validation
            //if (hookMethod.GetParameters().Length != 2)
            //{
            //    throw new InvalidOperationException("Invalid parameters. " +
            //        "Hook method expects (int value, ref int field) parameters");
            //}

            //Type parameterValueType = hookMethod.GetParameters()[0].ParameterType;

            //if (!parameterValueType.IsAssignableFrom(field.FieldType))
            //{
            //    throw new InvalidCastException("Invalid parameter type. " +
            //        $"expected {field.FieldType} but got {parameterValueType}");
            //}

            //Type fieldReferenceType = hookMethod.GetParameters()[1].ParameterType;

            //if (!fieldReferenceType.IsAssignableFrom(field.FieldType.MakeByRefType()))
            //{
            //    throw new InvalidCastException("Invalid parameter type. " +
            //        $"expected {field.FieldType} but got {fieldReferenceType}");
            //}

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
            
            var isPublic = field.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic;
            var isStatic = field.IsStatic ? BindingFlags.Static : BindingFlags.Instance;

            CodeInstruction[] assignmentInterceptBlock =
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldtoken, field.DeclaringType),
                new CodeInstruction(OpCodes.Call, getTypeFromHandle),
                new CodeInstruction(OpCodes.Ldstr, field.Name),
                new CodeInstruction(OpCodes.Ldc_I4, (int)(isPublic | isStatic)),
                new CodeInstruction(OpCodes.Callvirt, GetField),
                new CodeInstruction(OpCodes.Call, hookMethod.MakeGenericMethod(field.FieldType)),

            };

            CodeInstruction[] assignmentInterceptBlockStatic =
            {
                new CodeInstruction(OpCodes.Ldnull),
                new CodeInstruction(OpCodes.Ldtoken, field.DeclaringType),
                new CodeInstruction(OpCodes.Call, getTypeFromHandle),
                new CodeInstruction(OpCodes.Ldstr, field.Name),
                new CodeInstruction(OpCodes.Ldc_I4, (int)(isPublic | isStatic)),
                new CodeInstruction(OpCodes.Callvirt, GetField),
                new CodeInstruction(OpCodes.Call, hookMethod.MakeGenericMethod(field.FieldType)),
            };

            CodeInstruction[] byRefInterceptBlockPre =
            {
                new CodeInstruction(OpCodes.Ldsflda, tempField),
            };

            CodeInstruction[] byRefInterceptBlockPost =
            {
                new CodeInstruction(OpCodes.Ldsfld, tempField),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldflda, field),
                new CodeInstruction(OpCodes.Call, hookMethod),
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
