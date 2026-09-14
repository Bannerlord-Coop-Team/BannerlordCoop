using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CoopMcpServer;

public interface IOwnedProcessFactory
{
    IOwnedProcess Start(ProcessStartInfo startInfo);
}

public sealed class OwnedProcessFactory : IOwnedProcessFactory
{
    public IOwnedProcess Start(ProcessStartInfo startInfo)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Owned process jobs require Windows.");
        var job = Native.CreateJobObject(IntPtr.Zero, null);
        if (job.IsInvalid) throw new Win32Exception();
        Native.ProcessInformation created = default;
        Process process = null;
        try
        {
            var limits = new Native.ExtendedLimits();
            limits.Basic.LimitFlags = 0x2000; // Kill descendants on ownership-handle close; breakaway is not allowed.
            if (!Native.SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<Native.ExtendedLimits>()))
                throw new Win32Exception();
            var startup = new Native.StartupInfo { Size = Marshal.SizeOf<Native.StartupInfo>() };
            string command = QuoteArgument(startInfo.FileName) + " " + string.Join(" ", startInfo.ArgumentList.Select(QuoteArgument));
            if (!Native.CreateProcess(startInfo.FileName, new StringBuilder(command), IntPtr.Zero, IntPtr.Zero, false,
                0x4, IntPtr.Zero, startInfo.WorkingDirectory, ref startup, out created))
                throw new Win32Exception();
            // Suspend before assignment so no child can escape the owned job during startup.
            if (!Native.AssignProcessToJobObject(job, created.Process)) throw new Win32Exception();
            process = Process.GetProcessById((int)created.ProcessId);
            var owned = new OwnedProcess(process, job);
            if (Native.ResumeThread(created.Thread) == uint.MaxValue) throw new Win32Exception();
            return owned;
        }
        catch
        {
            if (created.Process != IntPtr.Zero)
            {
                Native.TerminateProcess(created.Process, 1);
                Native.WaitForSingleObject(created.Process, 5000);
            }
            process?.Dispose();
            job.Dispose();
            throw;
        }
        finally
        {
            if (created.Thread != IntPtr.Zero) Native.CloseHandle(created.Thread);
            if (created.Process != IntPtr.Zero) Native.CloseHandle(created.Process);
        }
    }

    public static string QuoteArgument(string value)
    {
        var result = new StringBuilder("\"");
        int slashes = 0;
        foreach (char character in value)
        {
            if (character == '\\') { slashes++; continue; }
            result.Append('\\', character == '"' ? (slashes * 2) + 1 : slashes);
            result.Append(character);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }

    private sealed class OwnedProcess : IOwnedProcess
    {
        private readonly Process process;
        private readonly SafeFileHandle job;
        private readonly object gate = new();
        private bool disposed;
        public int Pid { get; }
        public DateTime StartedUtc { get; }
        public bool IsAlive
        {
            get { lock (gate) return !disposed && !process.HasExited && process.StartTime.ToUniversalTime() == StartedUtc; }
        }
        public bool IsTreeAlive
        {
            get
            {
                lock (gate)
                {
                    if (disposed) return false;
                    if (!Native.QueryInformationJobObject(job, 1, out var accounting,
                        (uint)Marshal.SizeOf<Native.Accounting>(), IntPtr.Zero)) throw new Win32Exception();
                    return accounting.ActiveProcesses != 0;
                }
            }
        }
        public OwnedProcess(Process process, SafeFileHandle job)
        {
            this.process = process;
            this.job = job;
            Pid = process.Id;
            StartedUtc = process.StartTime.ToUniversalTime();
        }
        public async Task StopAsync(TimeSpan grace)
        {
            var elapsed = Stopwatch.StartNew();
            while (IsTreeAlive && elapsed.Elapsed < grace) await Task.Delay(50);
            if (!IsTreeAlive) return;
            if (!Native.TerminateJobObject(job, 1)) throw new Win32Exception();
            elapsed.Restart();
            while (IsTreeAlive && elapsed.Elapsed < TimeSpan.FromSeconds(5)) await Task.Delay(50);
            if (IsTreeAlive) throw new IOException("Owned job still contains processes after termination; retry stop_run.");
        }
        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                job.Dispose();
                process.Dispose();
            }
        }
    }

    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct ProcessInformation
        { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct StartupInfo
        {
            public int Size; public string Reserved, Desktop, Title;
            public uint X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
            public short ShowWindow, ReservedSize; public IntPtr ReservedBytes, StdInput, StdOutput, StdError;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct BasicLimits
        {
            public long PerProcessTime, PerJobTime; public uint LimitFlags;
            public UIntPtr MinimumWorkingSet, MaximumWorkingSet; public uint ActiveProcessLimit;
            public UIntPtr Affinity; public uint PriorityClass, SchedulingClass;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct IoCounters
        { public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
        [StructLayout(LayoutKind.Sequential)] internal struct ExtendedLimits
        {
            public BasicLimits Basic; public IoCounters Io;
            public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemory, PeakJobMemory;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct Accounting
        {
            public long TotalUserTime, TotalKernelTime, PeriodUserTime, PeriodKernelTime;
            public uint PageFaults, TotalProcesses, ActiveProcesses, TerminatedProcesses;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern SafeFileHandle CreateJobObject(IntPtr attributes, string name);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool SetInformationJobObject(SafeFileHandle job, int kind, ref ExtendedLimits value, uint size);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool QueryInformationJobObject(SafeFileHandle job, int kind, out Accounting value, uint size, IntPtr returned);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool TerminateJobObject(SafeFileHandle job, uint code);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes, bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInformation created);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern uint ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool TerminateProcess(IntPtr process, uint code);
        [DllImport("kernel32.dll")] internal static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll")] internal static extern bool CloseHandle(IntPtr handle);
    }
}
