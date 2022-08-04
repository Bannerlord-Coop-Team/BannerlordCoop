using NLog;
using NLog.Targets;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Coop.Mod
{
    [Target("MbLog")]
    public class MbLogTarget : TargetWithLayout
    {
        private static readonly Color Yellow = Color.FromUint(0xFFFF00);
        private static readonly Color Red = Color.FromUint(0xFF0000);
        protected override void Write(LogEventInfo logEvent)
        {
            Color textColor = Color.White;
            if (logEvent.Level == LogLevel.Warn)
            {
                textColor = Yellow;
            }
            else if (logEvent.Level >= LogLevel.Error)
            {
                textColor = Red;
            }

            InformationManager.DisplayMessage(
                new InformationMessage(Layout.Render(logEvent), textColor));
        }
    }
}
