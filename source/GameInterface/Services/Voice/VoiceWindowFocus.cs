using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GameInterface.Services.Voice;

public interface IVoiceWindowFocus
{
    bool IsFocused { get; }
}

public sealed class VoiceWindowFocus : IVoiceWindowFocus
{
    private readonly int processId;

    public VoiceWindowFocus()
    {
        using (var process = Process.GetCurrentProcess()) processId = process.Id;
    }

    public bool IsFocused
    {
        get
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out uint foregroundProcess);
            return foregroundProcess == processId;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
}
