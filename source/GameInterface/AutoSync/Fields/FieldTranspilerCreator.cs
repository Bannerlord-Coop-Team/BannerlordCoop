using Common.Logging;
using Common.Network;
using Common.PacketHandlers;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Collections;
using System.Linq;
using ProtoBuf;
using ProtoBuf.Meta;

namespace GameInterface.AutoSync.Fields;
public class FieldTranspilerCreator
{
    private readonly TypeBuilder typeBuilder;

    private readonly FieldBuilder loggerField;
    private readonly IObjectManager objectManager;
    private readonly Dictionary<FieldInfo, MethodInfo> interceptMap;

    public TypeInfo NestedEnumeratorType { get; }

    public FieldTranspilerCreator(IObjectManager objectManager, ModuleBuilder moduleBuilder, Type type, int typeId, FieldInfo[] interceptFields, Dictionary<FieldInfo, MethodInfo> interceptMap)
    {
        this.objectManager = objectManager;
        this.interceptMap = interceptMap;

        typeBuilder = moduleBuilder.DefineType($"{type.Name}_Transpilers",
            TypeAttributes.Public |
            TypeAttributes.Class |
            TypeAttributes.AutoClass |
            TypeAttributes.AnsiClass |
            TypeAttributes.BeforeFieldInit |
            TypeAttributes.AutoLayout,
            null);


        loggerField = typeBuilder.DefineField("logger", typeof(ILogger), FieldAttributes.Private | FieldAttributes.InitOnly | FieldAttributes.Static);

        CreateStaticCtor();

        NestedEnumeratorType = CreateNestedEnumeratorType(moduleBuilder, typeId, interceptFields);

        CreateTranspiler(typeBuilder, NestedEnumeratorType);

        var ctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard | CallingConventions.HasThis,
            null);

        var il = ctorBuilder.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, AccessTools.Constructor(typeof(object)));

        il.Emit(OpCodes.Ret);
    }

    private void CreateStaticCtor()
    {
        var cctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Static,
            CallingConventions.Standard,
            null);

        var il = cctorBuilder.GetILGenerator();

        il.Emit(OpCodes.Call, AccessTools.Method(typeof(LogManager), nameof(LogManager.GetLogger)).MakeGenericMethod(typeBuilder));
        il.Emit(OpCodes.Stsfld, loggerField);

        il.Emit(OpCodes.Ret);
    }

    private TypeInfo CreateNestedEnumeratorType(ModuleBuilder moduleBuilder, int typeId, FieldInfo[] interceptFields)
    {
        var nested_enumerable = moduleBuilder.DefineType($"internal_enumerable_{typeId}",
            TypeAttributes.Public |
            TypeAttributes.BeforeFieldInit |
            TypeAttributes.Sealed |
            TypeAttributes.AutoClass |
            TypeAttributes.AnsiClass,
            typeof(object),
            new Type[] { typeof(IEnumerator<CodeInstruction>), typeof(IEnumerable<CodeInstruction>), typeof(IDisposable) });

        var stateField = nested_enumerable.DefineField("state", typeof(int), FieldAttributes.Private); // <>1__state
        var currentField = nested_enumerable.DefineField("current", typeof(CodeInstruction), FieldAttributes.Private); // <>2__current
        var instructionsField = nested_enumerable.DefineField("instructions", typeof(IEnumerable<CodeInstruction>), FieldAttributes.Public); // instructions
        var instructionsEnumeratorField = nested_enumerable.DefineField("instructionsEnumerator", typeof(IEnumerator<CodeInstruction>), FieldAttributes.Private); // <>s__1
        var instructionsCurrentField = nested_enumerable.DefineField("instructionsCurrent", typeof(CodeInstruction), FieldAttributes.Private); // <instr>5__2

        CreateEnumeratorCtor(nested_enumerable, stateField, instructionsField, instructionsEnumeratorField);
        var finallyMethod = CreateFinallyMethod(nested_enumerable, stateField, instructionsEnumeratorField);

        var disposeMethod = CreateDisposeFunction(nested_enumerable, finallyMethod, stateField);
        nested_enumerable.DefineMethodOverride(disposeMethod, AccessTools.Method(typeof(IDisposable), nameof(IDisposable.Dispose)));

        var currentProperty = nested_enumerable.DefineProperty("Current", PropertyAttributes.None, typeof(CodeInstruction), null);
        var getPropertyMethod = CreateGetCurrent(nested_enumerable, currentField);

        currentProperty.SetGetMethod(getPropertyMethod);
        nested_enumerable.DefineMethodOverride(currentProperty.GetMethod, AccessTools.PropertyGetter(typeof(IEnumerator<CodeInstruction>), nameof(IEnumerator<CodeInstruction>.Current)));

        var getCurrentObject = CreateGetCurrentGeneric(nested_enumerable, currentField);
        nested_enumerable.DefineMethodOverride(getCurrentObject, AccessTools.PropertyGetter(typeof(IEnumerator), nameof(IEnumerator.Current)));

        var resetMethod = CreateReset(nested_enumerable);
        nested_enumerable.DefineMethodOverride(resetMethod, AccessTools.Method(typeof(IEnumerator), nameof(IEnumerator.Reset)));

        var getEnumeratorMethod = CreateGetEnumerator(nested_enumerable);
        nested_enumerable.DefineMethodOverride(getEnumeratorMethod, AccessTools.Method(typeof(IEnumerable<CodeInstruction>), nameof(IEnumerable<CodeInstruction>.GetEnumerator)));

        var getEnumeratorMethodGeneric = CreateGetEnumeratorGeneric(nested_enumerable, getEnumeratorMethod);
        nested_enumerable.DefineMethodOverride(getEnumeratorMethodGeneric, AccessTools.Method(typeof(IEnumerable), nameof(IEnumerable.GetEnumerator)));

        var moveNextMethod = CreateMoveNext(typeId,
            interceptFields,
            nested_enumerable,
            stateField,
            instructionsField,
            instructionsEnumeratorField,
            currentField,
            finallyMethod);
        nested_enumerable.DefineMethodOverride(moveNextMethod, AccessTools.Method(typeof(IEnumerator), nameof(IEnumerator.MoveNext)));

        return nested_enumerable.CreateTypeInfo();
    }

    private void CreateEnumeratorCtor(TypeBuilder typeBuilder, FieldBuilder stateField, FieldBuilder instructionsField, FieldBuilder instructionsEnumeratorField)
    {
        var ctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard | CallingConventions.HasThis,
            new Type[] { typeof(IEnumerable<CodeInstruction>) });

        var il = ctorBuilder.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, AccessTools.Constructor(typeof(object)));

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4, -3);
        il.Emit(OpCodes.Stfld, stateField);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, instructionsField);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, instructionsField);
        il.Emit(OpCodes.Callvirt, AccessTools.Method(typeof(IEnumerable<CodeInstruction>), nameof(IEnumerable<CodeInstruction>.GetEnumerator)));
        il.Emit(OpCodes.Stfld, instructionsEnumeratorField);

        il.Emit(OpCodes.Ret);
    }

    private MethodBuilder CreateDisposeFunction(TypeBuilder typeBuilder, MethodBuilder finallyMethod, FieldBuilder stateField)
    {
        var disposeBuilder = typeBuilder.DefineMethod(nameof(IDisposable.Dispose),
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            null,
            null);


        var il = disposeBuilder.GetILGenerator();

        var fieldLocal = il.DeclareLocal(stateField.FieldType);
        var finallyLabel = il.DefineLabel();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, stateField);
        il.Emit(OpCodes.Stloc, fieldLocal);

        il.Emit(OpCodes.Ldloc, fieldLocal);
        il.Emit(OpCodes.Ldc_I4, -3);
        il.Emit(OpCodes.Beq, finallyLabel);

        il.Emit(OpCodes.Ldloc, fieldLocal);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Beq, finallyLabel);

        il.Emit(OpCodes.Ret);


        il.MarkLabel(finallyLabel);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, finallyMethod);


        il.Emit(OpCodes.Ret);

        return disposeBuilder;
    }

    private MethodBuilder CreateFinallyMethod(TypeBuilder typeBuilder, FieldBuilder stateField, FieldBuilder instructionsEnumeratorField)
    {
        var finallyBuilder = typeBuilder.DefineMethod("Finally",
            MethodAttributes.Private | MethodAttributes.HideBySig,
            null,
            null);

        var il = finallyBuilder.GetILGenerator();

        var returnLabel = il.DefineLabel();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_M1);
        il.Emit(OpCodes.Stfld, stateField);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, instructionsEnumeratorField);
        il.Emit(OpCodes.Brfalse, returnLabel);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, instructionsEnumeratorField);
        il.Emit(OpCodes.Callvirt, AccessTools.Method(typeof(IDisposable), nameof(IDisposable.Dispose)));


        il.MarkLabel(returnLabel);
        il.Emit(OpCodes.Ret);

        return finallyBuilder;
    }

    private MethodBuilder CreateGetCurrent(TypeBuilder typeBuilder, FieldBuilder currentField)
    {
        var getCurrentBuilder = typeBuilder.DefineMethod("get_Current",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.SpecialName,
            typeof(CodeInstruction),
            null);

        var il = getCurrentBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, currentField);
        il.Emit(OpCodes.Ret);

        return getCurrentBuilder;
    }

    private MethodBuilder CreateGetCurrentGeneric(TypeBuilder typeBuilder, FieldBuilder currentField)
    {
        var getCurrentBuilder = typeBuilder.DefineMethod("get_Current",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.SpecialName,
            typeof(object),
            null);

        var il = getCurrentBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, currentField);
        il.Emit(OpCodes.Box, currentField.FieldType);
        il.Emit(OpCodes.Ret);

        return getCurrentBuilder;
    }

    private MethodBuilder CreateReset(TypeBuilder typeBuilder)
    {
        var resetBuilder = typeBuilder.DefineMethod(nameof(IEnumerator<CodeInstruction>.Reset),
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot,
            null,
            null);

        var il = resetBuilder.GetILGenerator();
        il.Emit(OpCodes.Newobj, typeof(NotImplementedException));
        il.Emit(OpCodes.Throw);

        return resetBuilder;
    }

    private MethodBuilder CreateMoveNext(
        int typeId,
        FieldInfo[] interceptFields,
        TypeBuilder typeBuilder,
        FieldBuilder stateField,
        FieldBuilder instructionsField,
        FieldBuilder instructionsEnumeratorField,
        FieldBuilder currentField,
        MethodBuilder finallyMethod)
    {
        var moveNextBuilder = typeBuilder.DefineMethod(nameof(IEnumerator<CodeInstruction>.MoveNext),
            MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
            typeof(bool),
            null);

        var il = moveNextBuilder.GetILGenerator();


        var currentLocal = il.DeclareLocal(typeof(CodeInstruction));

        //var returnLabel = il.DefineLabel();
        //var initLabel = il.DefineLabel();
        var nextLabel = il.DefineLabel();
        var storeCurrent = il.DefineLabel();

        var fieldLabels = interceptFields.Select(field => il.DefineLabel()).ToArray();

        //var tryBlockStart = il.BeginExceptionBlock();

        il.MarkLabel(nextLabel);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, instructionsEnumeratorField);
        il.Emit(OpCodes.Callvirt, AccessTools.Method(typeof(IEnumerator), nameof(IEnumerator.MoveNext)));
        il.Emit(OpCodes.Brtrue, storeCurrent);

        // return false
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(storeCurrent);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, instructionsEnumeratorField);
        il.Emit(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(IEnumerator<CodeInstruction>), nameof(IEnumerator<CodeInstruction>.Current)));
        il.Emit(OpCodes.Stloc, currentLocal);

        for (var i = 0; i < interceptFields.Length; i++)
        {
            il.Emit(OpCodes.Ldloc, currentLocal);

            il.Emit(OpCodes.Ldtoken, interceptFields[i].DeclaringType);
            il.Emit(OpCodes.Call, AccessTools.Method(typeof(Type), nameof(Type.GetTypeFromHandle)));

            il.Emit(OpCodes.Ldstr, interceptFields[i].Name);
            il.Emit(OpCodes.Call, AccessTools.Method(typeof(AccessTools), nameof(AccessTools.Field), new Type[] { typeof(Type), typeof(string) }));

            il.Emit(OpCodes.Call, AccessTools.Method(typeof(CodeInstructionExtensions), nameof(CodeInstructionExtensions.StoresField)));
            il.Emit(OpCodes.Brfalse, fieldLabels[i]);

            il.Emit(OpCodes.Ldsfld, AccessTools.Field(typeof(OpCodes), nameof(OpCodes.Call)));

            MethodBuilder fieldIntercept;

            if (RuntimeTypeModel.Default.CanSerialize(interceptFields[i].FieldType))
            {
                fieldIntercept = CreateInterceptByValue(typeId, i, interceptFields[i]);
            }
            else if (objectManager.IsTypeManaged(interceptFields[i].FieldType))
            {
                fieldIntercept = CreateInterceptByRef(typeId, i, interceptFields[i]);
            }
            else
            {
                throw new NotSupportedException(
                    $"{interceptFields[i].FieldType} is not serializable or managed by the {nameof(IObjectManager)}. " +
                    $"Create a registry for this type or make this type serializable using a surrogate");
            }

            interceptMap.Add(interceptFields[i], fieldIntercept);

            il.Emit(OpCodes.Ldtoken, fieldIntercept.DeclaringType);
            il.Emit(OpCodes.Call, AccessTools.Method(typeof(Type), nameof(Type.GetTypeFromHandle)));
            il.Emit(OpCodes.Ldstr, fieldIntercept.Name);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Call, AccessTools.Method(typeof(AccessTools), nameof(AccessTools.Method), new Type[] { typeof(Type), typeof(string), typeof(Type[]), typeof(Type[]) }));

            il.Emit(OpCodes.Newobj, AccessTools.Constructor(typeof(CodeInstruction), new Type[] { typeof(OpCode), typeof(object) }));

            // Store existing labels in new intercept
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldloc, currentLocal);
            il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(CodeInstruction), nameof(CodeInstruction.labels)));
            il.Emit(OpCodes.Stfld, AccessTools.Field(typeof(CodeInstruction), nameof(CodeInstruction.labels)));

            // Store existing blocks in new intercept
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldloc, currentLocal);
            il.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(CodeInstruction), nameof(CodeInstruction.blocks)));
            il.Emit(OpCodes.Stfld, AccessTools.Field(typeof(CodeInstruction), nameof(CodeInstruction.blocks)));

            il.Emit(OpCodes.Stloc, currentLocal);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldloc, currentLocal);
            il.Emit(OpCodes.Stfld, currentField);

            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Ret);

            il.MarkLabel(fieldLabels[i]);
        }

        // Return true
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloc, currentLocal);
        il.Emit(OpCodes.Stfld, currentField);

        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);

        return moveNextBuilder;
    }

    private MethodBuilder CreateGetEnumerator(TypeBuilder typeBuilder)
    {
        var getEnumerableBuilder = typeBuilder.DefineMethod(nameof(IEnumerable<CodeInstruction>.GetEnumerator),
            MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
            typeof(IEnumerator<CodeInstruction>),
            null);

        var il = getEnumerableBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ret);

        return getEnumerableBuilder;
    }

    private MethodBuilder CreateGetEnumeratorGeneric(TypeBuilder typeBuilder, MethodBuilder getEnumeratorMethod)
    {
        var getEnumerableGenericBuilder = typeBuilder.DefineMethod(nameof(IEnumerable.GetEnumerator),
            MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
            typeof(IEnumerator),
            null);

        var il = getEnumerableGenericBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, getEnumeratorMethod);
        il.Emit(OpCodes.Ret);

        return getEnumerableGenericBuilder;
    }

    private MethodBuilder CreateInterceptByValue(int typeId, int propId, FieldInfo field)
    {
        var methodBuilder = typeBuilder.DefineMethod($"{field.DeclaringType.Name}_{field.Name}_intercept",
            MethodAttributes.Public | MethodAttributes.Static,
            null,
            new Type[] { field.DeclaringType, field.FieldType });
        var instanceParam = methodBuilder.DefineParameter(0, ParameterAttributes.In, "instance");
        var valueParam = methodBuilder.DefineParameter(1, ParameterAttributes.In, "value");

        var il = methodBuilder.GetILGenerator();

        IsClientCheck(il, field);

        var networkLocal = TryResolve<INetwork>(il);

        var objectManagerLocal = TryResolve<IObjectManager>(il);
        var idLocal = TryGetId(il, OpCodes.Ldarg_0, objectManagerLocal);

        il.Emit(OpCodes.Ldloc, networkLocal);
        il.Emit(OpCodes.Ldloc, idLocal);
        il.Emit(OpCodes.Ldc_I4, typeId);
        il.Emit(OpCodes.Ldc_I4, propId);

        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Box, field.FieldType);
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(RawSerializer), nameof(RawSerializer.Serialize)));

        il.Emit(OpCodes.Newobj, AccessTools.Constructor(typeof(FieldAutoSyncPacket), new Type[] { typeof(string), typeof(int), typeof(int), typeof(byte[]) }));
        il.Emit(OpCodes.Box, typeof(FieldAutoSyncPacket));
        il.Emit(OpCodes.Callvirt, AccessTools.Method(typeof(INetwork), nameof(INetwork.SendAll), new Type[] { typeof(IPacket) }));

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, field);
        il.Emit(OpCodes.Ret);

        return methodBuilder;
    }

    private MethodBuilder CreateInterceptByRef(int typeId, int propId, FieldInfo field)
    {
        var methodBuilder = typeBuilder.DefineMethod($"{field.DeclaringType.Name}_{field.Name}_intercept",
            MethodAttributes.Public | MethodAttributes.Static,
            null,
            new Type[] { field.DeclaringType, field.FieldType });
        var instanceParam = methodBuilder.DefineParameter(0, ParameterAttributes.In, "instance");
        var valueParam = methodBuilder.DefineParameter(1, ParameterAttributes.In, "value");

        var il = methodBuilder.GetILGenerator();

        IsClientCheck(il, field);

        var networkLocal = TryResolve<INetwork>(il);
        var objectManagerLocal = TryResolve<IObjectManager>(il);

        var idLocal = TryGetId(il, OpCodes.Ldarg_0, objectManagerLocal);
        var valueIdLocal = TryGetId(il, OpCodes.Ldarg_1, objectManagerLocal);

        il.Emit(OpCodes.Ldloc, networkLocal);
        il.Emit(OpCodes.Ldloc, idLocal);
        il.Emit(OpCodes.Ldc_I4, typeId);
        il.Emit(OpCodes.Ldc_I4, propId);

        il.Emit(OpCodes.Ldloc, valueIdLocal);

        il.Emit(OpCodes.Call, AccessTools.Method(typeof(RawSerializer), nameof(RawSerializer.Serialize)));

        il.Emit(OpCodes.Newobj, AccessTools.Constructor(typeof(FieldAutoSyncPacket), new Type[] { typeof(string), typeof(int), typeof(int), typeof(byte[]) }));
        il.Emit(OpCodes.Box, typeof(FieldAutoSyncPacket));
        il.Emit(OpCodes.Callvirt, AccessTools.Method(typeof(INetwork), nameof(INetwork.SendAll), new Type[] { typeof(IPacket) }));

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, field);
        il.Emit(OpCodes.Ret);

        return methodBuilder;
    }

    private LocalBuilder TryResolve<T>(ILGenerator il)
    {
        var validLabel = il.DefineLabel();
        var local = il.DeclareLocal(typeof(T));

        // Attempt resolve
        il.Emit(OpCodes.Ldloca, local);
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(ContainerProvider), nameof(ContainerProvider.TryResolve)).MakeGenericMethod(typeof(T)));
        il.Emit(OpCodes.Brtrue, validLabel);

        // Log error
        il.Emit(OpCodes.Ldsfld, loggerField);
        il.Emit(OpCodes.Ldstr, $"Unable to resolve {nameof(T)}");
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(ILogger), nameof(ILogger.Error), new Type[] { typeof(string) }));

        // Return
        il.Emit(OpCodes.Ret);

        il.MarkLabel(validLabel);

        return local;
    }

    private LocalBuilder TryGetId(ILGenerator il, OpCode argOpcode, LocalBuilder objectManagerLocal)
    {
        var validLabel = il.DefineLabel();
        var idLocal = il.DeclareLocal(typeof(string));

        il.Emit(OpCodes.Ldloc, objectManagerLocal);

        // load instance
        il.Emit(argOpcode);
        il.Emit(OpCodes.Ldloca, idLocal);

        // Try resolve instance id
        il.Emit(OpCodes.Callvirt, AccessTools.Method(typeof(IObjectManager), nameof(IObjectManager.TryGetId)));
        il.Emit(OpCodes.Brtrue, validLabel);

        // Log error
        il.Emit(OpCodes.Ldsfld, loggerField);
        il.Emit(OpCodes.Ldstr, $"Could not resolve id");
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(ILogger), nameof(ILogger.Error), new Type[] { typeof(string) }));

        // Return
        il.Emit(OpCodes.Ret);

        il.MarkLabel(validLabel);

        return idLocal;
    }

    private void IsClientCheck(ILGenerator il, MemberInfo field)
    {
        il.Emit(OpCodes.Call, AccessTools.PropertyGetter(typeof(ModInformation), nameof(ModInformation.IsClient)));
        var notClientLabel = il.DefineLabel();

        il.Emit(OpCodes.Brfalse, notClientLabel);

        // Log error
        il.Emit(OpCodes.Ldsfld, loggerField);
        il.Emit(OpCodes.Ldstr, $"Client attempted to change {field.Name}");
        il.Emit(OpCodes.Call, AccessTools.Method(typeof(ILogger), nameof(ILogger.Error), new Type[] { typeof(string) }));

        // Return
        il.Emit(OpCodes.Ret);

        il.MarkLabel(notClientLabel);
    }

    private void CreateTranspiler(TypeBuilder typeBuilder, TypeInfo innerEnumerator)
    {
        var transpilerBuilder = typeBuilder.DefineMethod("Transpiler",
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
            typeof(IEnumerable<CodeInstruction>),
            new Type[] { typeof(IEnumerable<CodeInstruction>) });

        var il = transpilerBuilder.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Newobj, innerEnumerator.GetConstructor(new Type[] { typeof(IEnumerable<CodeInstruction>) }));
        il.Emit(OpCodes.Ret);
    }


    public Type Build()
    {
        return typeBuilder.CreateTypeInfo();
    }
}
