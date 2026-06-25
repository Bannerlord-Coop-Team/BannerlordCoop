using System;
using System.Reflection;
using System.Runtime.InteropServices;
using GameInterface.Utils;
using Xunit;

namespace GameInterface.Tests.AutoSync;

/// <summary>
/// Covers <see cref="FragileDetourGuard"/>: the guard skips a Harmony detour only for a "fragile no-op"
/// method - fragile (its detour would corrupt its inline x64 unwind info, the #1539 freeze), does no field
/// store (so no transpiler can rewrite it), and has no prefix/postfix. The skip decision is tested with an
/// injected fragility predicate so it is deterministic on any platform; the real RtlLookupFunctionEntry-based
/// IsFragile is Windows-x64 only.
/// </summary>
public class FragileDetourGuardTests
{
    private class Sample
    {
        public int _x;
        public void StoreScalar(int v) { _x = v; }      // stfld _x        -> does a field store
        public int ReadScalar() => _x;                  // ldfld _x        -> reads, no store
        public bool ConstFalse() => false;              // no member ref   -> the deadlock stub shape
        public void CallSomething() { string.Concat("a", "b"); } // call, no store -> has a real body
    }

    private static MethodBase M(string name) => typeof(Sample).GetMethod(name);

    private static readonly Func<MethodBase, bool> Fragile = _ => true;
    private static readonly Func<MethodBase, bool> NotFragile = _ => false;
    private static readonly Func<MethodBase, bool> NoPatch = _ => false;
    private static readonly Func<MethodBase, bool> LoadBearingPatch = _ => true;

    // --- DoesFieldStore: the no-op proxy (a field-set transpiler can only rewrite a method that does a store) ---

    [Fact]
    public void DoesFieldStore_true_for_a_field_store()
        => Assert.True(FragileDetourGuard.DoesFieldStore(M(nameof(Sample.StoreScalar))));

    [Fact]
    public void DoesFieldStore_false_for_a_field_read()
        => Assert.False(FragileDetourGuard.DoesFieldStore(M(nameof(Sample.ReadScalar))));

    [Fact]
    public void DoesFieldStore_false_for_a_constant_stub()
        => Assert.False(FragileDetourGuard.DoesFieldStore(M(nameof(Sample.ConstFalse))));

    // --- ShouldSkipDetour: skip iff fragile AND no field store AND no load-bearing prefix/postfix ---

    [Fact]
    public void Skips_a_fragile_noop_stub()
        => Assert.True(FragileDetourGuard.ShouldSkipDetour(M(nameof(Sample.ConstFalse)), Fragile, NoPatch));

    // The robustness invariant: a method that stores a field is never skipped, even when fragile, because a
    // transpiler could rewrite that store (it is load-bearing).
    [Fact]
    public void Keeps_a_fragile_method_that_stores_a_field()
        => Assert.False(FragileDetourGuard.ShouldSkipDetour(M(nameof(Sample.StoreScalar)), Fragile, NoPatch));

    // A non-fragile method is never skipped - its detour is safe.
    [Fact]
    public void Keeps_a_non_fragile_method()
        => Assert.False(FragileDetourGuard.ShouldSkipDetour(M(nameof(Sample.ConstFalse)), NotFragile, NoPatch));

    // A prefix/postfix on a tiny stub makes its detour load-bearing; never skip it.
    [Fact]
    public void Keeps_a_fragile_stub_that_has_a_prefix_or_postfix()
        => Assert.False(FragileDetourGuard.ShouldSkipDetour(M(nameof(Sample.ConstFalse)), Fragile, LoadBearingPatch));

    // --- IsFragile platform contract ---

    [Fact]
    public void IsFragile_is_false_for_null()
        => Assert.False(FragileDetourGuard.IsFragile(null));

    // Off Windows x64 there is no inline unwind info to corrupt, so nothing is fragile and the guard is inert
    // (the path CI runs on).
    [Fact]
    public void IsFragile_is_false_off_windows()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;
        Assert.False(FragileDetourGuard.IsFragile(M(nameof(Sample.ConstFalse))));
    }

    // On Windows the guard actually fires; pin the load-bearing invariant where it runs: a method with a real
    // body/frame must never be flagged fragile, or the guard could skip a load-bearing detour. CI skips this.
    [Fact]
    public void IsFragile_is_false_for_a_method_with_a_real_body_on_windows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;
        Assert.False(FragileDetourGuard.IsFragile(M(nameof(Sample.CallSomething))));
    }
}
