using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using EnvDTE80;

namespace DebugServerLauncher
{
    /// <summary>
    /// COM message filter: while registered on the calling (STA) thread, calls
    /// that a busy Visual Studio rejects with RPC_E_CALL_REJECTED are retried
    /// continuously at the COM channel level instead of failing back to us.
    /// Without it, a VS that is actively debugging (pumping debug events)
    /// rejects nearly every discrete call attempt.
    /// </summary>
    [ComImport, Guid("00000016-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IOleMessageFilter
    {
        [PreserveSig] int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);
        [PreserveSig] int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);
        [PreserveSig] int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
    }

    internal sealed class MessageFilter : IOleMessageFilter
    {
        [DllImport("ole32.dll")]
        private static extern int CoRegisterMessageFilter(IOleMessageFilter newFilter, out IOleMessageFilter oldFilter);

        public static bool TryRegister()
        {
            try
            {
                IOleMessageFilter old;
                return CoRegisterMessageFilter(new MessageFilter(), out old) == 0;
            }
            catch
            {
                return false;
            }
        }

        public static void Revoke()
        {
            try
            {
                IOleMessageFilter old;
                CoRegisterMessageFilter(null, out old);
            }
            catch
            {
                // nothing to revoke
            }
        }

        int IOleMessageFilter.HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo)
        {
            return 0; // SERVERCALL_ISHANDLED
        }

        int IOleMessageFilter.RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType)
        {
            return 150; // retry after 150 ms, for both SERVERCALL_REJECTED and RETRYLATER
        }

        int IOleMessageFilter.MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType)
        {
            return 2; // PENDINGMSG_WAITDEFPROCESS
        }
    }

    /// <summary>
    /// Finds the running Visual Studio instance via the COM Running Object Table
    /// and attaches its CoreCLR debug engine to a process through DTE automation.
    /// The explicit engine (EnvDTE80.Process2.Attach2) is the whole point: plain
    /// Process.Attach() uses the session's Desktop CLR default and fatals against
    /// a .NET 6 target — the exact error this tool exists to avoid.
    /// </summary>
    internal static class VisualStudioAttacher
    {
        private const string CoreClrEngineName = "Managed (.NET Core, .NET 5+)";
        private const string CoreClrEngineGuid = "{2E36F1D4-B23C-435D-AB41-18E608940038}"; // version-proof fallback

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

        /// <summary>
        /// The DTE of the VS instance whose open solution matches
        /// <paramref name="solutionMatch"/> (invariant substring, e.g. "Coop.sln"),
        /// else the only instance running, else null.
        /// </summary>
        public static DTE2 FindVisualStudio(string solutionMatch, out int instanceCount)
        {
            instanceCount = 0;
            DTE2 matched = null;
            DTE2 single = null;

            IRunningObjectTable rot;
            if (GetRunningObjectTable(0, out rot) != 0)
            {
                return null;
            }
            IEnumMoniker enumMoniker;
            rot.EnumRunning(out enumMoniker);
            var monikers = new IMoniker[1];
            while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
            {
                IBindCtx ctx;
                CreateBindCtx(0, out ctx);
                string displayName = null;
                try { monikers[0].GetDisplayName(ctx, null, out displayName); } catch { }
                if (displayName == null || !displayName.StartsWith("!VisualStudio.DTE"))
                {
                    continue;
                }
                object dteObj;
                if (rot.GetObject(monikers[0], out dteObj) != 0)
                {
                    continue;
                }
                var dte = dteObj as DTE2;
                if (dte == null)
                {
                    continue;
                }
                instanceCount++;
                single = dte;
                if (matched == null && SolutionPathOf(dte).IndexOf(solutionMatch, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matched = dte;
                }
            }
            return matched ?? (instanceCount == 1 ? single : null);
        }

        /// <summary>
        /// Attaches VS's CoreCLR engine to <paramref name="pid"/>, retrying until
        /// <paramref name="timeoutSec"/>. Returns null on success, else the error.
        /// </summary>
        public static string Attach(DTE2 dte, int pid, int timeoutSec)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(timeoutSec);
            string lastError = "process " + pid + " never appeared in Visual Studio's process list";
            bool filtered = MessageFilter.TryRegister();
            try
            {
                while (DateTime.UtcNow < deadline)
                {
                    try { System.Diagnostics.Process.GetProcessById(pid); }
                    catch { return "engine process " + pid + " exited before the attach"; }

                    // The CoreCLR engine cannot connect before the runtime is up in
                    // the target (the native engine boots first, the CLR moments
                    // later); attaching native-only by racing it would leave managed
                    // breakpoints unbound forever.
                    if (!IsCoreClrLoaded(pid))
                    {
                        lastError = "the .NET runtime never loaded in process " + pid;
                        Thread.Sleep(400);
                        continue;
                    }

                    try
                    {
                        EnvDTE.Processes processes = dte.Debugger.LocalProcesses;
                        int count = processes.Count;
                        for (int i = 1; i <= count; i++)
                        {
                            EnvDTE.Process process = processes.Item(i);
                            if (process.ProcessID != pid)
                            {
                                continue;
                            }
                            var process2 = (Process2)process;
                            foreach (string engine in new[] { CoreClrEngineName, CoreClrEngineGuid })
                            {
                                try
                                {
                                    process2.Attach2(engine);
                                    return null;
                                }
                                catch (COMException ex)
                                {
                                    lastError = "Attach2(" + engine + "): 0x" + ex.HResult.ToString("X8") + " " + ex.Message;
                                }
                            }
                        }
                    }
                    catch (COMException ex)
                    {
                        lastError = "DTE busy: 0x" + ex.HResult.ToString("X8");
                    }
                    Thread.Sleep(400);
                }
                return lastError;
            }
            finally
            {
                if (filtered)
                {
                    MessageFilter.Revoke();
                }
            }
        }

        private static string SolutionPathOf(DTE2 dte)
        {
            bool filtered = MessageFilter.TryRegister();
            try
            {
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    try
                    {
                        return dte.Solution.FullName ?? "";
                    }
                    catch (COMException)
                    {
                        Thread.Sleep(250);
                    }
                }
                return "";
            }
            finally
            {
                if (filtered)
                {
                    MessageFilter.Revoke();
                }
            }
        }

        private static bool IsCoreClrLoaded(int pid)
        {
            try
            {
                foreach (System.Diagnostics.ProcessModule module in System.Diagnostics.Process.GetProcessById(pid).Modules)
                {
                    if (module.ModuleName.Equals("coreclr.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // transient module-enumeration failures during process startup
            }
            return false;
        }
    }
}
