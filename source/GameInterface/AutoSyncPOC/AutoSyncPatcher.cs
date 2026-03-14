using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace GameInterface.AutoSyncPOC;

public interface IAutoSyncPatcher
{
    void PatchFields();
}

internal class AutoSyncPatcher
{
    private static readonly Harmony harmony = new Harmony("AutoSyncPOC");

    private static readonly MethodInfo TranspilerInfo = AccessTools.Method(typeof(AutoSyncPatcher), nameof(Transpiler));
    private readonly IFieldRegistry fieldRegistry;

    public AutoSyncPatcher(IFieldRegistry fieldRegistry)
    {
        this.fieldRegistry = fieldRegistry;
    }

    public void PatchFields()
    {
        var types = fieldRegistry.Fields.Select(x => x.DeclaringType).Distinct();
        var methods = types.SelectMany(x => x.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)).Distinct();

        foreach (MethodInfo method in methods)
        {
            try
            {
                harmony.Patch(method, transpiler: TranspilerInfo);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create patch for {method.Name}: {ex.Message}");
            }
        }
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        if (!ContainerProvider.TryResolve<INetworkIdRegistry>(out var networkIdRegistry)) return instructions;
        if (!ContainerProvider.TryResolve<IFieldRegistry>(out var fieldRegistry)) return instructions;

        return InjectIntercept(instructions, networkIdRegistry, fieldRegistry);
    }

    private static IEnumerable<CodeInstruction> InjectIntercept(IEnumerable<CodeInstruction> instructions, INetworkIdRegistry networkIdRegistry, IFieldRegistry fieldRegistry)
    {
        foreach (CodeInstruction instr in instructions)
        {
            if (instr.opcode == OpCodes.Stfld && instr.operand is FieldInfo fieldInfo && fieldRegistry.TryGetId(fieldInfo, out var id))
            {
                yield return new CodeInstruction(OpCodes.Ldc_I4, id);

                var operandType = fieldInfo.FieldType;
                var newInstr = instr.Clone();

                newInstr.opcode = OpCodes.Call;
                newInstr.operand = SelectIntercept(networkIdRegistry, operandType).MakeGenericMethod(fieldInfo.DeclaringType, fieldInfo.FieldType);

                yield return newInstr;
                continue;
            }


            yield return instr;
        }
    }

    private static MethodInfo SelectIntercept(INetworkIdRegistry networkIdRegistry, Type fieldType)
    {
        if (ModInformation.IsClient)
            return AccessTools.Method(typeof(AutoSyncPatcher), nameof(ClientIntercept));

        if (networkIdRegistry.IsTypeManaged(fieldType))
            return AccessTools.Method(typeof(AutoSyncPatcher), nameof(ServerReferenceIntercept));

        return AccessTools.Method(typeof(AutoSyncPatcher), nameof(ServerValueIntercept));
    }

    public static void ServerValueIntercept<TInstance, TField>(TInstance instance, TField value, int fieldId)
    {
        if (!ContainerProvider.TryResolve<IFieldProcessor>(out var fieldValueProcessor)) return;

        fieldValueProcessor.SendValue(instance, fieldId, value);
        fieldValueProcessor.SetValue(instance, fieldId, value);
    }

    public static void ServerReferenceIntercept<TInstance, TField>(TInstance instance, TField value, int fieldId)
    {
        if (!ContainerProvider.TryResolve<IFieldProcessor>(out var fieldValueProcessor)) return;

        fieldValueProcessor.SendReference(instance, fieldId, value);
        fieldValueProcessor.SetValue(instance, fieldId, value);
    }

    // Client will never send to server, so value and references can be treated the same
    public static void ClientIntercept<TInstance, TField>(TInstance instance, TField value, int fieldId)
    {
        if (!ContainerProvider.TryResolve<ILogger>(out var logger)) return;

        if (!ContainerProvider.TryResolve<IFieldProcessor>(out var fieldValueProcessor)) return;

        if (!CallOriginalPolicy.IsOriginalAllowed())
        {
            fieldValueProcessor.SetValue(instance, fieldId, value);
            return;
        }

        if (!ContainerProvider.TryResolve<IFieldRegistry>(out var fieldRegistry)) return;

        if (!fieldRegistry.TryGetField(fieldId, out var fieldInfo)) return;

        logger.Error(
            "Client attempted to set {fieldInfo}\n" +
            "Callstack: {callstack}",
            fieldInfo,
            Environment.StackTrace);
    }
}
