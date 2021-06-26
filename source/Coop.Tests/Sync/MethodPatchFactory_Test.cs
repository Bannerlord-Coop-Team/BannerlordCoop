using System;
using System.Collections.Generic;
using HarmonyLib;
using Sync.Behaviour;
using Sync.Call;
using Sync.Patch;
using Xunit;

namespace Coop.Tests.Sync
{
    [Collection(
        "UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class MethodPatchFactory_Test
    {
        [ThreadStatic] private static List<PatchedInvokable> m_DispatcherCalls;

        private readonly Foo m_Foo = new Foo();

        public MethodPatchFactory_Test()
        {
            m_DispatcherCalls = new List<PatchedInvokable>();
        }

        private static bool DispatcherTrue(
            PatchedInvokable patchedInvokable,
            object instance,
            params object[] args)
        {
            m_DispatcherCalls.Add(patchedInvokable);
            return true;
        }

        private static bool DispatcherFalse(
            PatchedInvokable patchedInvokable,
            object instance,
            params object[] args)
        {
            m_DispatcherCalls.Add(patchedInvokable);
            return false;
        }

        private static void DispatcherVoid(
            PatchedInvokable patchedInvokable,
            object instance,
            params object[] args)
        {
            m_DispatcherCalls.Add(patchedInvokable);
        }

        private static bool DispatcherCallOriginal(
            PatchedInvokable patchedInvokable,
            object instance,
            params object[] args)
        {
            m_DispatcherCalls.Add(patchedInvokable);
            patchedInvokable.Invoke(EOriginator.RemoteAuthority, instance, args);
            return false;
        }

        [Fact]
        private void ArgumentsAreForwarded()
        {
            // Generate prefix for ThreeArgs
            var method = AccessTools.Method(typeof(Foo), nameof(Foo.ThreeArgs));
            var dispatcher = AccessTools.Method(
                typeof(MethodPatchFactory_Test),
                nameof(DispatcherCallOriginal));
            var patch = new PatchedInvokable(method, typeof(Foo));
            var prefix = MethodPatchFactory<MethodPatchFactory_Test>.GeneratePatch(
                "Prefix",
                patch,
                dispatcher);

            // Call
            var iArg = 42;
            var fArg = 43.0f;
            var dArg = 44.0;
            var ret = prefix.Invoke(m_Foo, new object[] {m_Foo, iArg, fArg, dArg});
            Assert.IsType<bool>(ret);
            Assert.False((bool) ret); // DispatcherCallOriginal returns false
            Assert.Single(m_DispatcherCalls);
            Assert.NotNull(m_DispatcherCalls[0]);
            Assert.Same(patch, m_DispatcherCalls[0]);

            // Verify original was called
            Assert.Single(m_Foo.CallHistory);
            Assert.Equal(Foo.EMethod.ThreeArgs, m_Foo.CallHistory[0]);
            Assert.Single(m_Foo.ArgsHistory);
            Assert.Equal(3, m_Foo.ArgsHistory[0].Length);

            // Verify individual args where forwarded
            Assert.NotNull(m_Foo.ArgsHistory[0][0]);
            Assert.IsType<int>(m_Foo.ArgsHistory[0][0]);
            Assert.Equal(iArg, (int) m_Foo.ArgsHistory[0][0]);
            Assert.NotNull(m_Foo.ArgsHistory[0][1]);
            Assert.IsType<float>(m_Foo.ArgsHistory[0][1]);
            Assert.Equal(fArg, (float) m_Foo.ArgsHistory[0][1]);
            Assert.NotNull(m_Foo.ArgsHistory[0][2]);
            Assert.IsType<double>(m_Foo.ArgsHistory[0][2]);
            Assert.Equal(dArg, (double) m_Foo.ArgsHistory[0][2]);

            MethodPatchFactory<MethodPatchFactory_Test>.UnpatchAll();
        }

        [Fact]
        private void GeneratePrefixWithMultipleArgsWorks()
        {
            // Generate prefix for SingleArg
            var method = AccessTools.Method(typeof(Foo), nameof(Foo.ThreeArgs));
            var dispatcher = AccessTools.Method(
                typeof(MethodPatchFactory_Test),
                nameof(DispatcherTrue));
            var patch = new PatchedInvokable(method, typeof(Foo));
            var prefix = MethodPatchFactory<MethodPatchFactory_Test>.GeneratePatch(
                "Prefix",
                patch,
                dispatcher);

            // Call
            var ret = prefix.Invoke(m_Foo, new object[] {m_Foo, 42, 43.0f, 44.0});
            Assert.IsType<bool>(ret);
            Assert.True((bool) ret); // AlwaysCallOriginal

            MethodPatchFactory<MethodPatchFactory_Test>.UnpatchAll();
        }

        private class Foo
        {
            public enum EMethod
            {
                SingleArg,
                ThreeArgs
            }

            public List<EMethod> CallHistory { get; } = new List<EMethod>();
            public List<object[]> ArgsHistory { get; } = new List<object[]>();

            public void SingleArg(int i)
            {
                CallHistory.Add(EMethod.SingleArg);
                ArgsHistory.Add(new object[] {i});
            }

            public void ThreeArgs(int i, float f, double d)
            {
                CallHistory.Add(EMethod.ThreeArgs);
                ArgsHistory.Add(new object[] {i, f, d});
            }
        }
    }
}