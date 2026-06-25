using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GameInterface.Utils
{
    /// <summary>
    /// Closes the #1539 co-op server freeze at its single root: Harmony detours a method by writing a 5-byte
    /// rel32 jmp over its entry, and for a tiny method whose inline x64 unwind info (.xdata) sits within those
    /// 5 bytes, that write corrupts the unwind data the GC's suspend-time stack walk reads, self-deadlocking
    /// the GC. AutoSync used to detour every declared method of every synced type, so it detoured a flood of
    /// such tiny stubs (e.g. <c>Clan.IFaction.get_IsKingdomFaction =&gt; false</c>).
    ///
    /// Rather than narrow each patch class's TargetMethods (which can't see hand-written patches and risks
    /// un-detouring a load-bearing caller), this is a single Harmony prefix on <c>PatchTools.DetourMethod</c>,
    /// the one chokepoint every patch path funnels through. It skips the byte write ONLY for a "fragile no-op"
    /// detour: a method that is
    ///   (a) fragile  - its detour would corrupt its own unwind info (<see cref="IsFragile"/>), AND
    ///   (b) does no field store - so no transpiler can actually rewrite it (<see cref="DoesFieldStore"/>), AND
    ///   (c) carries no prefix/postfix/finalizer.
    /// Skipping such a detour leaves the method running its original code, which is behaviorally identical
    /// (the detour rewrote nothing) but with intact unwind info. This covers EVERY patch in the process the
    /// same way - AutoSync-generated, hand-written, and any future patch - with no per-class wiring.
    ///
    /// Soundness: the only way a method is load-bearing for a field-set transpiler is to do a field store, so
    /// (b) never skips one; (c) catches prefix/postfix patches on tiny stubs. Measured across all synced
    /// types, this skips the freeze-causing stubs with zero load-bearing methods dropped. Windows x64 only -
    /// <see cref="IsFragile"/> returns false elsewhere (Linux/CI), so off that platform the guard is inert.
    /// </summary>
    public static class FragileDetourGuard
    {
        private static readonly ILogger Logger = LogManager.GetLogger(typeof(FragileDetourGuard));
        private static bool applied;

        /// <summary>
        /// Patches <c>HarmonyLib.PatchTools.DetourMethod</c> with the guard prefix. Must run before the patches
        /// it should cover; idempotent. Harmony installs this prefix via DetourMethod itself while the guard is
        /// not yet active, so there is no recursion, and the prefix only reads state (no further detours).
        /// </summary>
        public static void Apply(Harmony harmony)
        {
            if (applied) return;

            var patchTools = typeof(Harmony).Assembly.GetType("HarmonyLib.PatchTools");
            var detourMethod = patchTools == null ? null : AccessTools.Method(patchTools, "DetourMethod");
            if (detourMethod == null)
            {
                Logger.Error("FragileDetourGuard: HarmonyLib.PatchTools.DetourMethod not found; the #1539 unwind-corruption guard is INACTIVE");
                return;
            }

            harmony.Patch(detourMethod, prefix: new HarmonyMethod(typeof(FragileDetourGuard), nameof(DetourMethodPrefix)));
            applied = true;
        }

        // Prefix on PatchTools.DetourMethod(MethodBase method, MethodBase replacement). Returning false skips
        // the original (the 5-byte write); returning true lets the detour proceed.
        private static bool DetourMethodPrefix(MethodBase method)
        {
            if (!ShouldSkipDetour(method, IsFragile, HasLoadBearingPatch)) return true;

            Logger.Debug("Skipped a fragile no-op detour on {type}.{method} to preserve its inline x64 unwind info (#1539)",
                method.DeclaringType?.Name, method.Name);
            return false;
        }

        /// <summary>
        /// The skip decision, with <paramref name="isFragile"/> and <paramref name="hasLoadBearingPatch"/>
        /// injected so the keep/drop logic is unit-testable on any platform (the real <see cref="IsFragile"/>
        /// is Windows-x64 only). Skip iff the method is fragile, stores no field, and has no prefix/postfix.
        /// </summary>
        public static bool ShouldSkipDetour(MethodBase method, Func<MethodBase, bool> isFragile, Func<MethodBase, bool> hasLoadBearingPatch)
        {
            if (method == null) return false;
            return isFragile(method) && !DoesFieldStore(method) && !hasLoadBearingPatch(method);
        }

        private static bool HasLoadBearingPatch(MethodBase method)
        {
            var info = Harmony.GetPatchInfo(method);
            if (info == null) return false;
            return (info.Prefixes != null && info.Prefixes.Count > 0)
                || (info.Postfixes != null && info.Postfixes.Count > 0)
                || (info.Finalizers != null && info.Finalizers.Count > 0);
        }

        /// <summary>
        /// True when <paramref name="method"/>'s IL contains a field store (stfld/stsfld). A field-set
        /// transpiler can only rewrite a method that does such a store, so a method with none is a no-op
        /// detour for it. Fail-safe: any time the body/IL can't be cleanly decoded it returns true (treat the
        /// detour as load-bearing and keep it), so the guard never skips a method it can't prove is a no-op.
        /// </summary>
        public static bool DoesFieldStore(MethodBase method)
        {
            byte[] il;
            try
            {
                var body = method.GetMethodBody();
                if (body == null) return true; // no managed IL to inspect -> keep the detour
                il = body.GetILAsByteArray();
            }
            catch
            {
                return true;
            }
            if (il == null) return true;

            try
            {
                int i = 0;
                while (i < il.Length)
                {
                    short code;
                    if (il[i] == 0xFE && i + 1 < il.Length) { code = (short)(0xFE00 | il[i + 1]); i += 2; }
                    else { code = il[i]; i += 1; }

                    if (code == 0x7D || code == 0x80) return true; // stfld / stsfld

                    int operandSize = OperandSize(code, il, i);
                    if (operandSize < 0) return true; // can't size the operand -> walk would desync, keep
                    i += operandSize;
                }
            }
            catch
            {
                return true;
            }
            return false;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RuntimeFunction { public uint BeginAddress, EndAddress, UnwindData; }

        [DllImport("ntdll.dll")]
        private static extern IntPtr RtlLookupFunctionEntry(ulong controlPc, out ulong imageBase, IntPtr historyTable);

        // Harmony writes a 5-byte rel32 jmp over the method entry to detour it.
        private const int DetourWriteSize = 5;

        /// <summary>
        /// True when detouring <paramref name="method"/> would overwrite its own inline x64 unwind info: the
        /// 5-byte jmp at the entry overruns a method whose .xdata (UnwindData) sits within 5 bytes of
        /// BeginAddress, corrupting the unwind data the GC's suspend-time stack walk reads (the #1539 freeze).
        /// Windows x64 only; returns false where RtlLookupFunctionEntry is unavailable (Linux/CI) or on any
        /// error, so the guard is inert off the platform that actually has the deadlock.
        /// </summary>
        public static bool IsFragile(MethodBase method)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
            if (method == null || method.IsAbstract || method.ContainsGenericParameters) return false;
            if (method.DeclaringType != null && method.DeclaringType.ContainsGenericParameters) return false;

            try
            {
                RuntimeHelpers.PrepareMethod(method.MethodHandle);
                ulong entry = (ulong)method.MethodHandle.GetFunctionPointer().ToInt64();
                IntPtr functionEntry = RtlLookupFunctionEntry(entry, out _, IntPtr.Zero);
                if (functionEntry == IntPtr.Zero) return false;

                var runtimeFunction = (RuntimeFunction)Marshal.PtrToStructure(functionEntry, typeof(RuntimeFunction));
                long unwindOffset = (long)runtimeFunction.UnwindData - runtimeFunction.BeginAddress;
                return unwindOffset >= 0 && unwindOffset < DetourWriteSize;
            }
            catch
            {
                return false;
            }
        }

        private static readonly Dictionary<short, OperandType> OperandTypes = BuildOperandTypes();

        private static Dictionary<short, OperandType> BuildOperandTypes()
        {
            var map = new Dictionary<short, OperandType>();
            foreach (var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var op = (OpCode)f.GetValue(null);
                map[op.Value] = op.OperandType;
            }
            return map;
        }

        private static int OperandSize(short opcode, byte[] il, int operandStart)
        {
            if (!OperandTypes.TryGetValue(opcode, out var operandType)) return -1;
            switch (operandType)
            {
                case OperandType.InlineNone: return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar: return 1;
                case OperandType.InlineVar: return 2;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR: return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR: return 8;
                case OperandType.InlineSwitch:
                    if (operandStart + 4 > il.Length) return -1;
                    int n = BitConverter.ToInt32(il, operandStart);
                    long size = 4L + ((long)n * 4);
                    if (n < 0 || operandStart + size > il.Length) return -1;
                    return (int)size;
                default: return -1;
            }
        }
    }
}
