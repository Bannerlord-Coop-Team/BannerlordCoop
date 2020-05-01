namespace Coop.Game.CLI
{
    using System.Runtime.InteropServices;
    public static class Console
    {
        [DllImport(
            "Rgl.dll",
            CallingConvention = CallingConvention.StdCall,
            EntryPoint = "?toggle_imgui_console_visibility@rglCommand_line_manager@@QEAAXXZ")]
        private static extern void rgl_ToggleImguiConsoleVisibility();

        public static void Toggle()
        {
            rgl_ToggleImguiConsoleVisibility();
        }
    }
}
