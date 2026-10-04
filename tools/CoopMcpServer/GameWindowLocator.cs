using System.Runtime.InteropServices;
using System.Text;

namespace CoopMcpServer;

public sealed record OccludingWindow(string Title, int Pid);

// X/Y/Width/Height are physical virtual-screen pixels, as gdigrab desktop offsets expect them.
public sealed record CaptureRegion(string Mode, string FallbackReason, long WindowHandle, string WindowTitle, int X, int Y, int Width, int Height,
    bool Occluded = false, OccludingWindow[] OccludingWindows = null);

public sealed record ScreenRect(int Left, int Top, int Right, int Bottom)
{
    public bool Intersects(ScreenRect other) => Left < other.Right && other.Left < Right && Top < other.Bottom && other.Top < Bottom;
}

public sealed record WindowSnapshot(string Title, int Pid, ScreenRect Bounds, bool Visible, bool Minimized, bool Cloaked, bool ClickThrough);

public interface IGameWindowLocator
{
    CaptureRegion Resolve(int pid);
}

public sealed class GameWindowLocator : IGameWindowLocator
{
    public const string WindowCapture = "window_capture";
    public const string WindowRegion = "window_region";
    public const string PrimaryDisplay = "primary_display";
    public const int MaxOccluders = 16;
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
            var client = new ScreenRect(origin.X, origin.Y, origin.X + rect.Right, origin.Y + rect.Bottom);
            int left = Math.Max(client.Left, Native.GetSystemMetrics(Native.XVirtualScreen));
            int top = Math.Max(client.Top, Native.GetSystemMetrics(Native.YVirtualScreen));
            int right = Math.Min(client.Right, Native.GetSystemMetrics(Native.XVirtualScreen) + Native.GetSystemMetrics(Native.CxVirtualScreen));
            int bottom = Math.Min(client.Bottom, Native.GetSystemMetrics(Native.YVirtualScreen) + Native.GetSystemMetrics(Native.CyVirtualScreen));
            // libx264 yuv420p needs even dimensions.
            int width = (right - left) & ~1, height = (bottom - top) & ~1;
            if (width < MinimumSize || height < MinimumSize) return Primary("The instance window client area is off-screen or too small.");
            var occluders = FindOccluders(client, WindowsAbove(window));
            return new CaptureRegion(WindowRegion, null, window.ToInt64(), Title(window), left, top, width, height, occluders.Length > 0, occluders);
        }
        finally
        {
            if (previous != IntPtr.Zero) Native.SetThreadDpiAwarenessContext(previous);
        }
    }

    // Windows above the target in z-order that are actually drawn over its client area; click-through overlays are ignored.
    public static OccludingWindow[] FindOccluders(ScreenRect target, IEnumerable<WindowSnapshot> windowsAbove) =>
        windowsAbove.Where(w => w.Visible && !w.Minimized && !w.Cloaked && !w.ClickThrough &&
                w.Bounds.Right > w.Bounds.Left && w.Bounds.Bottom > w.Bounds.Top && w.Bounds.Intersects(target))
            .Take(MaxOccluders).Select(w => new OccludingWindow(w.Title, w.Pid)).ToArray();

    private static IEnumerable<WindowSnapshot> WindowsAbove(IntPtr window)
    {
        // Bounded because the z-order can change while it is walked.
        int walked = 0;
        for (IntPtr above = Native.GetWindow(window, Native.GwHwndPrev); above != IntPtr.Zero && walked++ < 4096; above = Native.GetWindow(above, Native.GwHwndPrev))
        {
            Native.GetWindowThreadProcessId(above, out uint owner);
            bool cloaked = Native.DwmGetWindowAttribute(above, Native.DwmwaCloaked, out int cloak, sizeof(int)) == 0 && cloak != 0;
            if (Native.DwmGetWindowAttribute(above, Native.DwmwaExtendedFrameBounds, out Native.Rect bounds, Marshal.SizeOf<Native.Rect>()) != 0)
                Native.GetWindowRect(above, out bounds);
            long exStyle = Native.GetWindowLongPtr(above, Native.GwlExStyle).ToInt64();
            bool clickThrough = (exStyle & Native.WsExTransparent) != 0 && (exStyle & Native.WsExLayered) != 0;
            yield return new WindowSnapshot(Title(above), (int)owner, new ScreenRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom),
                Native.IsWindowVisible(above), Native.IsIconic(above), cloaked, clickThrough);
        }
    }

    private static string Title(IntPtr window)
    {
        var title = new StringBuilder(256);
        Native.GetWindowText(window, title, title.Capacity);
        return title.ToString();
    }

    private static CaptureRegion Primary(string reason) => new(PrimaryDisplay, reason, 0, null, 0, 0,
        Native.GetSystemMetrics(Native.CxScreen) & ~1, Native.GetSystemMetrics(Native.CyScreen) & ~1, false, Array.Empty<OccludingWindow>());

    private static class Native
    {
        public static readonly IntPtr PerMonitorAwareV2 = new(-4);
        public const uint GwHwndPrev = 3, GwOwner = 4;
        public const int GwlExStyle = -20, DwmwaExtendedFrameBounds = 9, DwmwaCloaked = 14;
        public const long WsExTransparent = 0x20, WsExLayered = 0x80000;
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
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr window, ref Point point);
        [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
        [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Rect value, int size);
    }
}
