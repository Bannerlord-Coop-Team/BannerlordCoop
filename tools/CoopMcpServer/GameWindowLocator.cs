using System.Runtime.InteropServices;
using System.Text;

namespace CoopMcpServer;

// Physical virtual-screen pixels, as gdigrab desktop offsets expect them.
public sealed record CaptureRegion(string Mode, string FallbackReason, long WindowHandle, string WindowTitle, int X, int Y, int Width, int Height);

public interface IGameWindowLocator
{
    CaptureRegion Resolve(int pid);
}

public sealed class GameWindowLocator : IGameWindowLocator
{
    public const string WindowRegion = "window_region";
    public const string PrimaryDisplay = "primary_display";
    private const int MinimumSize = 16;

    public CaptureRegion Resolve(int pid)
    {
        IntPtr previous = Native.SetThreadDpiAwarenessContext(Native.PerMonitorAwareV2);
        try
        {
            IntPtr window = IntPtr.Zero;
            long bestArea = -1;
            Native.EnumWindows((handle, _) =>
            {
                Native.GetWindowThreadProcessId(handle, out uint owner);
                if (owner != pid || !Native.IsWindowVisible(handle) || Native.GetWindow(handle, Native.GwOwner) != IntPtr.Zero) return true;
                long area = Native.GetClientRect(handle, out var client) ? (long)(client.Right - client.Left) * (client.Bottom - client.Top) : 0;
                if (area > bestArea) { bestArea = area; window = handle; }
                return true;
            }, IntPtr.Zero);
            if (window == IntPtr.Zero) return Primary("No visible top-level window is owned by the instance process.");
            if (Native.IsIconic(window)) return Primary("The instance window is minimized.");
            var origin = new Native.Point();
            if (!Native.GetClientRect(window, out var rect) || !Native.ClientToScreen(window, ref origin))
                return Primary("The instance window client area could not be read.");
            int left = Math.Max(origin.X, Native.GetSystemMetrics(Native.XVirtualScreen));
            int top = Math.Max(origin.Y, Native.GetSystemMetrics(Native.YVirtualScreen));
            int right = Math.Min(origin.X + rect.Right, Native.GetSystemMetrics(Native.XVirtualScreen) + Native.GetSystemMetrics(Native.CxVirtualScreen));
            int bottom = Math.Min(origin.Y + rect.Bottom, Native.GetSystemMetrics(Native.YVirtualScreen) + Native.GetSystemMetrics(Native.CyVirtualScreen));
            // libx264 yuv420p needs even dimensions.
            int width = (right - left) & ~1, height = (bottom - top) & ~1;
            if (width < MinimumSize || height < MinimumSize) return Primary("The instance window client area is off-screen or too small.");
            var title = new StringBuilder(256);
            Native.GetWindowText(window, title, title.Capacity);
            return new CaptureRegion(WindowRegion, null, window.ToInt64(), title.ToString(), left, top, width, height);
        }
        finally
        {
            if (previous != IntPtr.Zero) Native.SetThreadDpiAwarenessContext(previous);
        }
    }

    private static CaptureRegion Primary(string reason) => new(PrimaryDisplay, reason, 0, null, 0, 0,
        Native.GetSystemMetrics(Native.CxScreen) & ~1, Native.GetSystemMetrics(Native.CyScreen) & ~1);

    private static class Native
    {
        public static readonly IntPtr PerMonitorAwareV2 = new(-4);
        public const uint GwOwner = 4;
        public const int CxScreen = 0, CyScreen = 1, XVirtualScreen = 76, YVirtualScreen = 77, CxVirtualScreen = 78, CyVirtualScreen = 79;
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
        public delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr window, ref Point point);
        [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
        [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    }
}
