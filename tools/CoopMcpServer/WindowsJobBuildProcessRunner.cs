using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using Native = CoopMcpServer.OwnedProcessFactory.Native;

namespace CoopMcpServer;

public interface IBuildProcessRunner
{
    Task RunAsync(ProcessStartInfo info, string artifacts, string project, CancellationToken cancellationToken);
}

public sealed class WindowsJobBuildProcessRunner : IBuildProcessRunner
{
    private readonly IBuildCleanupRecovery recovery;
    private readonly TimeSpan buildTimeout;
    private readonly TimeSpan cleanupTimeout;
    private readonly Func<string, Stream> openLog;

    public WindowsJobBuildProcessRunner(IBuildCleanupRecovery recovery)
        : this(recovery, TimeSpan.FromMinutes(20), TimeSpan.FromSeconds(5)) { }

    internal WindowsJobBuildProcessRunner(IBuildCleanupRecovery recovery, TimeSpan buildTimeout, TimeSpan cleanupTimeout, Func<string, Stream> openLog = null)
    {
        this.recovery = recovery;
        this.buildTimeout = buildTimeout;
        this.cleanupTimeout = cleanupTimeout;
        this.openLog = openLog ?? (path => new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true));
    }

    public async Task RunAsync(ProcessStartInfo info, string artifacts, string project, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Deployment builds require Windows.");
        await recovery.RecoverAsync();
        cancellationToken.ThrowIfCancellationRequested();
        var build = new BuildProcess(cleanupTimeout, openLog);
        try
        {
            build.OpenLogs(artifacts, project);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(buildTimeout);
            build.Start(info, timeout.Token);
            await build.WaitForExitAsync(timeout.Token);
            if (build.ExitCode != 0) throw new InvalidOperationException($"{project} build failed ({build.ExitCode}); inspect {artifacts}.");
            await build.DrainAsync(timeout.Token);
        }
        finally
        {
            try { await build.CleanupAsync(); }
            catch (Exception error)
            {
                // Keep all handles and streams reachable until cleanup is positively confirmed.
                recovery.Retain(build.CleanupAsync);
                throw new BuildCleanupException(recovery, error);
            }
        }
    }

    private sealed class BuildProcess
    {
        private readonly TimeSpan cleanupTimeout;
        private readonly Func<string, Stream> openLog;
        private readonly CancellationTokenSource stopReads = new();
        private SafeFileHandle job;
        private IntPtr process;
        private bool assigned;
        private Stream stdout, stderr;
        private NamedPipeServerStream outputPipe, errorPipe;
        private CancellationTokenSource reads;
        private Task output = Task.CompletedTask, errors = Task.CompletedTask;
        private Task logFinalization;

        public BuildProcess(TimeSpan cleanupTimeout, Func<string, Stream> openLog)
        {
            this.cleanupTimeout = cleanupTimeout;
            this.openLog = openLog;
        }

        public uint ExitCode
        {
            get
            {
                if (!GetExitCodeProcess(process, out uint code)) throw new Win32Exception();
                return code;
            }
        }

        public void OpenLogs(string artifacts, string project)
        {
            stdout = openLog(Path.Combine(artifacts, project + "-stdout.log"));
            stderr = openLog(Path.Combine(artifacts, project + "-stderr.log"));
        }

        public void Start(ProcessStartInfo info, CancellationToken token)
        {
            if (!info.RedirectStandardOutput || !info.RedirectStandardError || info.UseShellExecute)
                throw new InvalidOperationException("Builds require redirected stdout/stderr without shell execution.");
            job = Native.CreateJobObject(IntPtr.Zero, null);
            if (job.IsInvalid) throw new Win32Exception();
            var limits = new Native.ExtendedLimits();
            limits.Basic.LimitFlags = 0x2000;
            if (!Native.SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<Native.ExtendedLimits>())) throw new Win32Exception();
            using var outputWriter = OpenPipe(out outputPipe);
            using var errorWriter = OpenPipe(out errorPipe);
            reads = CancellationTokenSource.CreateLinkedTokenSource(token, stopReads.Token);
            output = outputPipe.CopyToAsync(stdout, reads.Token);
            errors = errorPipe.CopyToAsync(stderr, reads.Token);
            var startup = new StartupInfoEx();
            startup.Info.Size = Marshal.SizeOf<StartupInfoEx>();
            startup.Info.Flags = 0x100; // STARTF_USESTDHANDLES
            startup.Info.StdOutput = outputWriter.SafePipeHandle.DangerousGetHandle();
            startup.Info.StdError = errorWriter.SafePipeHandle.DangerousGetHandle();
            IntPtr size = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            startup.Attributes = Marshal.AllocHGlobal(size);
            IntPtr handles = Marshal.AllocHGlobal(IntPtr.Size * 2);
            bool initialized = false;
            Native.ProcessInformation created = default;
            try
            {
                if (!InitializeProcThreadAttributeList(startup.Attributes, 1, 0, ref size)) throw new Win32Exception();
                initialized = true;
                Marshal.WriteIntPtr(handles, startup.Info.StdOutput);
                Marshal.WriteIntPtr(handles, IntPtr.Size, startup.Info.StdError);
                // Only the two output writers may be inherited, never another build's handles.
                if (!UpdateProcThreadAttribute(startup.Attributes, 0, (IntPtr)0x20002, handles, (IntPtr)(IntPtr.Size * 2), IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception();
                string command = OwnedProcessFactory.QuoteArgument(info.FileName) + " " + string.Join(" ", info.ArgumentList.Select(OwnedProcessFactory.QuoteArgument));
                if (!CreateProcess(info.FileName, new StringBuilder(command), IntPtr.Zero, IntPtr.Zero, true,
                    0x08080004, IntPtr.Zero, info.WorkingDirectory, ref startup, out created)) throw new Win32Exception();
                process = created.Process;
                // Assignment happens before any build code can run or spawn descendants.
                if (!Native.AssignProcessToJobObject(job, process)) throw new Win32Exception();
                assigned = true;
                if (Native.ResumeThread(created.Thread) == uint.MaxValue) throw new Win32Exception();
            }
            finally
            {
                if (created.Thread != IntPtr.Zero) Native.CloseHandle(created.Thread);
                if (initialized) DeleteProcThreadAttributeList(startup.Attributes);
                Marshal.FreeHGlobal(startup.Attributes);
                Marshal.FreeHGlobal(handles);
            }
        }

        private NamedPipeClientStream OpenPipe(out NamedPipeServerStream reader)
        {
            string name = "CoopBuild-" + Guid.NewGuid().ToString("N");
            reader = new NamedPipeServerStream(name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            var writer = new NamedPipeClientStream(".", name, PipeDirection.Out, PipeOptions.None,
                System.Security.Principal.TokenImpersonationLevel.Anonymous, HandleInheritability.Inheritable);
            try
            {
                writer.Connect(5000);
                reader.WaitForConnection();
                return writer;
            }
            catch { writer.Dispose(); throw; }
        }

        private bool RootAlive()
        {
            if (process == IntPtr.Zero) return false;
            uint result = Native.WaitForSingleObject(process, 0);
            if (result == uint.MaxValue) throw new Win32Exception();
            return result != 0;
        }

        private bool TreeAlive()
        {
            if (job == null || job.IsInvalid) return false;
            if (!Native.QueryInformationJobObject(job, 1, out var accounting, (uint)Marshal.SizeOf<Native.Accounting>(), IntPtr.Zero)) throw new Win32Exception();
            return accounting.ActiveProcesses != 0;
        }

        public async Task WaitForExitAsync(CancellationToken token)
        {
            while (RootAlive()) await Task.Delay(25, token);
            token.ThrowIfCancellationRequested();
        }

        public Task DrainAsync(CancellationToken token) => Task.WhenAll(output, errors).WaitAsync(token);

        public async Task CleanupAsync()
        {
            using var deadline = new CancellationTokenSource(cleanupTimeout);
            if (TreeAlive() && !Native.TerminateJobObject(job, 1)) throw new Win32Exception();
            // Also own a suspended root when assignment failed before it joined the job.
            if (!assigned && RootAlive() && !Native.TerminateProcess(process, 1) && RootAlive()) throw new Win32Exception();
            while (TreeAlive() || RootAlive()) await Task.Delay(25, deadline.Token);
            stopReads.Cancel();
            try { await DrainAsync(deadline.Token); }
            catch (Exception) when (output.IsCompleted && errors.IsCompleted) { }
            // Disposal flushes buffered writes; start it once off-thread and retain it across retries.
            logFinalization ??= Task.WhenAll(
                Task.Run(async () => { if (stdout != null) await stdout.DisposeAsync(); }),
                Task.Run(async () => { if (stderr != null) await stderr.DisposeAsync(); }));
            await logFinalization.WaitAsync(deadline.Token);
            outputPipe?.Dispose();
            errorPipe?.Dispose();
            reads?.Dispose();
            stopReads.Dispose();
            if (process != IntPtr.Zero) { Native.CloseHandle(process); process = IntPtr.Zero; }
            job?.Dispose();
            job = null;
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct StartupInfoEx
    { public Native.StartupInfo Info; public IntPtr Attributes; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes, bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfoEx startup, out Native.ProcessInformation created);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(IntPtr process, out uint code);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool InitializeProcThreadAttributeList(IntPtr attributes, int count, int flags, ref IntPtr size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool UpdateProcThreadAttribute(IntPtr attributes, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returned);
    [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr attributes);
}
