using Common;
using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Utils;

/// <summary>
/// Redirects the engine's water clock read (scene clock tick * 0.0001) to a float we own, so wave time can be synced.
/// </summary>
/// <remarks>Redirected only while a synced naval battle drives the float; static because native memory is process-wide.</remarks>
internal static class NativeWaterClockPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(NativeWaterClockPatch));

    internal const string ModuleName = "TaleWorlds.Native.dll";

    // movsxd rax,[rbx+6064h] / lea rbp,[rip+table] / xorps xmm1,xmm1 / cvtsi2ss xmm1,qword [rbp+rax*8] / mulss xmm1,[rip+k]
    internal static readonly byte?[] Signature = Parse(
        "48 63 83 64 60 00 00 48 8D 2D ?? ?? ?? ?? 0F 57 C9 F3 48 0F 2A 4C C5 00 F3 0F 59 0D");
    internal const int PatchOffset = 17; // cvtsi2ss + mulss, 15 bytes
    internal const int PatchLength = 15;
    // The lea's disp32 and the mulss's disp32, each relative to the end of its instruction.
    internal const int TableDisplacementOffset = 10;
    internal const int TableInstructionEnd = 14;
    internal const int ScaleDisplacementOffset = 28;
    internal const int ScaleInstructionEnd = 32;
    // The int64 tick table holds one accumulated clock per rglTimer_type (MBCommon.TimeType); mission scenes read Mission.
    private const int MissionTimer = (int)MBCommon.TimeType.Mission;

    private const uint ImageScnMemExecute = 0x20000000;
    private const uint PageExecuteReadWrite = 0x40;
    private const uint PageReadWrite = 0x04;
    private const uint MemCommitReserve = 0x3000;
    private const uint MemRelease = 0x8000;

    private static readonly object gate = new object();
    private static readonly float[] buffer = new float[1];
    private static bool attempted;
    private static IntPtr site;
    private static IntPtr waterTimeSlot;
    private static IntPtr timeTable;
    private static float tickScale;
    private static byte[] originalCode;
    private static byte[] redirectCode;

    /// <summary>Whether the patch is installed: the signature matched once and the slot is allocated.</summary>
    public static bool IsActive => waterTimeSlot != IntPtr.Zero;

    /// <summary>Whether the engine currently reads the slot instead of its own clock.</summary>
    public static bool IsRedirected { get; private set; }

    /// <summary>Why the patch is or isn't installed, for the log and the debug command.</summary>
    public static string Status { get; private set; } = "not installed";

    /// <summary>The value the last <see cref="SetWaterTime"/> wrote.</summary>
    public static float WrittenTime { get; private set; }

    /// <summary>Finds the clock load and prepares the redirect once; leaves vanilla untouched unless the signature matches exactly once.</summary>
    public static bool TryApply()
    {
        lock (gate)
        {
            // Checked before the once-latch, so a server never latches and never touches native memory.
            if (!ModInformation.IsClient) return Disabled("not a client process", warn: false);
            if (attempted) return IsActive;
            attempted = true;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return Disabled("not Windows", warn: false);

            var module = FindModule();
            if (module == null) return Disabled($"{ModuleName} is not loaded", warn: true);

            return TryInstall(module);
        }
    }

    /// <summary>Sets the time the water simulation reads on its next update. Game thread only.</summary>
    /// <remarks>The unit is the engine's water clock: timer ticks (1e5 per second) * 0.0001, so 10 per timer second.</remarks>
    public static void SetWaterTime(float waterTime)
    {
        if (!IsActive) return;

        buffer[0] = waterTime;
        Marshal.Copy(buffer, 0, waterTimeSlot, 1);
        WrittenTime = waterTime;
    }

    /// <summary>Writes the slot, then points the clock load at it. Game thread only, the thread that runs the load.</summary>
    public static bool TryRedirect(float waterTime)
    {
        if (!IsActive) return false;
        if (!IsOnGameThread(nameof(TryRedirect))) return false;

        SetWaterTime(waterTime);
        if (IsRedirected) return true;
        if (!TryWriteCode(originalCode, redirectCode)) return false;

        IsRedirected = true;
        Logger.Debug("Water clock redirected on thread {ThreadId}", Thread.CurrentThread.ManagedThreadId);
        return true;
    }

    /// <summary>Puts the vanilla clock load back. Game thread only.</summary>
    public static void Restore()
    {
        if (!IsRedirected) return;
        if (!IsOnGameThread(nameof(Restore))) return;
        if (!TryWriteCode(redirectCode, originalCode)) return;

        IsRedirected = false;
        Logger.Debug("Water clock restored on thread {ThreadId}", Thread.CurrentThread.ManagedThreadId);
    }

    /// <summary>The exact value the vanilla load computes for a mission scene: (float)ticks * k, read from its own table and constant.</summary>
    public static bool TryReadVanillaMissionTime(out float waterTime)
    {
        waterTime = 0f;
        if (!IsActive) return false;

        float ticks = Marshal.ReadInt64(timeTable, MissionTimer * sizeof(long));
        waterTime = ticks * tickScale;
        return true;
    }

    // The site is only ever executed by the game thread, so a swap from any other thread could tear a running instruction.
    private static bool IsOnGameThread(string operation)
    {
        if (GameThread.Instance.IsGameThread) return true;

        Logger.Error("Water clock {Operation} refused off the game thread (thread {ThreadId})", operation, Thread.CurrentThread.ManagedThreadId);
        return false;
    }

    // Scans the executable sections, allocates the slot and captures both code variants; logs and frees on any failure.
    private static bool TryInstall(ProcessModule module)
    {
        var baseAddress = module.BaseAddress;
        var matches = FindInExecutableSections(baseAddress, 2);
        if (matches.Count != 1)
        {
            Logger.Warning("Water clock patch disabled: signature matched {Matches} time(s) in {Module} (file version {FileVersion}, " +
                "PE timestamp {Timestamp:X8}, image size {Size}); waves stay vanilla",
                matches.Count, ModuleName, DescribeFileVersion(module), ReadTimestamp(baseAddress), module.ModuleMemorySize);
            Status = $"disabled: signature matched {matches.Count} time(s)";
            return false;
        }

        var match = matches[0];
        var siteAddress = match + PatchOffset;
        var slot = AllocNear(baseAddress);
        if (slot == IntPtr.Zero) return Disabled("no free page within rel32 reach of the module", warn: true);

        if (!TryBuildRedirect(siteAddress.ToInt64(), slot.ToInt64(), out var code))
        {
            VirtualFree(slot, UIntPtr.Zero, MemRelease);
            return Disabled("slot out of rel32 range", warn: true);
        }

        originalCode = new byte[PatchLength];
        Marshal.Copy(siteAddress, originalCode, 0, PatchLength);
        redirectCode = code;
        timeTable = new IntPtr(ResolveRipTarget(match.ToInt64() + TableInstructionEnd, Marshal.ReadInt32(match, TableDisplacementOffset)));
        var scaleAddress = new IntPtr(ResolveRipTarget(match.ToInt64() + ScaleInstructionEnd, Marshal.ReadInt32(match, ScaleDisplacementOffset)));
        Marshal.Copy(scaleAddress, buffer, 0, 1);
        tickScale = buffer[0];
        site = siteAddress;
        waterTimeSlot = slot;
        Status = "installed";
        Logger.Information("Water clock patch installed at {Module}+0x{Rva:X} (slot 0x{Slot:X}, tick scale {Scale})",
            ModuleName, siteAddress.ToInt64() - baseAddress.ToInt64(), slot.ToInt64(), tickScale);
        return true;
    }

    // Swaps the 15 bytes at the site, only while they still hold the expected variant; false and a log line on any failure.
    private static bool TryWriteCode(byte[] expected, byte[] code)
    {
        var current = new byte[PatchLength];
        Marshal.Copy(site, current, 0, PatchLength);
        if (!SequenceEqual(current, expected))
        {
            Logger.Error("Water clock patch site holds unexpected bytes {Bytes}; left untouched", BitConverter.ToString(current));
            return false;
        }

        if (!VirtualProtect(site, (UIntPtr)PatchLength, PageExecuteReadWrite, out var old))
        {
            Logger.Error("Water clock patch could not unprotect its site (error {Error})", Marshal.GetLastWin32Error());
            return false;
        }

        Marshal.Copy(code, 0, site, PatchLength);
        if (!VirtualProtect(site, (UIntPtr)PatchLength, old, out _))
            Logger.Warning("Water clock patch could not restore its site's protection (error {Error})", Marshal.GetLastWin32Error());
        FlushInstructionCache(GetCurrentProcess(), site, (UIntPtr)PatchLength);
        return true;
    }

    // Records why the patch is off and logs it once.
    private static bool Disabled(string reason, bool warn)
    {
        Status = $"disabled: {reason}";
        if (warn) Logger.Warning("Water clock patch disabled: {Reason}; waves stay vanilla", reason);
        else Logger.Information("Water clock patch disabled: {Reason}; waves stay vanilla", reason);
        return false;
    }

    // The loaded native engine module, or null when this process has none.
    private static ProcessModule FindModule()
    {
        foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
        {
            if (string.Equals(module.ModuleName, ModuleName, StringComparison.OrdinalIgnoreCase)) return module;
        }
        return null;
    }

    // Scans only the executable sections the PE header lists, so no unmapped or guard page is ever read.
    private static List<IntPtr> FindInExecutableSections(IntPtr baseAddress, int maxMatches)
    {
        var matches = new List<IntPtr>();
        int ntHeaders = Marshal.ReadInt32(baseAddress, 0x3C);
        int sectionCount = Marshal.ReadInt16(baseAddress, ntHeaders + 6);
        int optionalHeaderSize = Marshal.ReadInt16(baseAddress, ntHeaders + 20);
        int sectionTable = ntHeaders + 24 + optionalHeaderSize;
        for (int i = 0; i < sectionCount && matches.Count < maxMatches; i++)
        {
            int header = sectionTable + (i * 40);
            uint characteristics = (uint)Marshal.ReadInt32(baseAddress, header + 36);
            if ((characteristics & ImageScnMemExecute) == 0) continue;

            int virtualSize = Marshal.ReadInt32(baseAddress, header + 8);
            int virtualAddress = Marshal.ReadInt32(baseAddress, header + 12);
            var section = new byte[virtualSize];
            Marshal.Copy(baseAddress + virtualAddress, section, 0, virtualSize);
            foreach (var offset in FindMatches(section, Signature, maxMatches - matches.Count))
                matches.Add(baseAddress + virtualAddress + offset);
        }
        return matches;
    }

    /// <summary>Allocates one zeroed read-write page below the module, within rel32 reach of its code.</summary>
    private static IntPtr AllocNear(IntPtr module)
    {
        long start = module.ToInt64() & ~0xFFFFL;
        for (long address = start - 0x10000; address > start - 0x70000000 && address > 0x10000; address -= 0x10000)
        {
            var page = VirtualAlloc(new IntPtr(address), (UIntPtr)0x1000, MemCommitReserve, PageReadWrite);
            if (page != IntPtr.Zero) return page;
        }
        return IntPtr.Zero;
    }

    /// <summary>Offsets where the signature matches, up to <paramref name="maxMatches"/>; anchored on its first fixed byte.</summary>
    internal static List<int> FindMatches(byte[] data, byte?[] signature, int maxMatches)
    {
        var matches = new List<int>();
        byte first = signature[0].Value;
        int last = data.Length - signature.Length;
        int i = 0;
        while (i <= last && matches.Count < maxMatches)
        {
            i = Array.IndexOf(data, first, i, last - i + 1);
            if (i < 0) break;
            if (IsMatch(data, i, signature)) matches.Add(i);
            i++;
        }
        return matches;
    }

    /// <summary>Encodes movss xmm1,[rip+slot] plus a 7-byte NOP for the site; false when the slot is beyond rel32 reach.</summary>
    internal static bool TryBuildRedirect(long siteAddress, long slotAddress, out byte[] code)
    {
        code = null;
        long displacement = slotAddress - (siteAddress + 8);
        if (displacement < int.MinValue || displacement > int.MaxValue) return false;

        var disp = BitConverter.GetBytes((int)displacement);
        // movss xmm1, [rip+disp32] then a 7-byte NOP
        code = new byte[] { 0xF3, 0x0F, 0x10, 0x0D, disp[0], disp[1], disp[2], disp[3], 0x0F, 0x1F, 0x80, 0, 0, 0, 0 };
        return true;
    }

    /// <summary>The address a rip-relative operand points at, given the end of its instruction.</summary>
    internal static long ResolveRipTarget(long instructionEnd, int displacement) => instructionEnd + displacement;

    /// <summary>Checks whether the signature matches at one offset; null bytes are wildcards.</summary>
    internal static bool IsMatch(byte[] data, int offset, byte?[] signature)
    {
        for (int j = 0; j < signature.Length; j++)
        {
            if (signature[j].HasValue && data[offset + j] != signature[j].Value) return false;
        }
        return true;
    }

    /// <summary>Parses "48 8D ?? ..." into bytes, with null for a wildcard.</summary>
    internal static byte?[] Parse(string signature)
    {
        var tokens = signature.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var bytes = new byte?[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
            bytes[i] = tokens[i] == "??" ? (byte?)null : Convert.ToByte(tokens[i], 16);
        return bytes;
    }

    // Compares two equal-length byte arrays.
    private static bool SequenceEqual(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    // The module's file version, which the engine DLL ships without, so a port can still tell builds apart by the timestamp.
    private static string DescribeFileVersion(ProcessModule module)
    {
        var version = module.FileVersionInfo?.FileVersion;
        return string.IsNullOrEmpty(version) ? "none" : version;
    }

    // The PE header's link timestamp.
    private static uint ReadTimestamp(IntPtr baseAddress) =>
        (uint)Marshal.ReadInt32(baseAddress, Marshal.ReadInt32(baseAddress, 0x3C) + 8);

    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protect);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protect, out uint old);
    [DllImport("kernel32.dll")] private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
}
