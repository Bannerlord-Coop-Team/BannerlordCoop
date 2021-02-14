using NLog;
using NLog.Targets;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Coop.Mod
{
    [Target("MbLog")]
    public class MbLogTarget : TargetWithLayout
    {
        protected override void Write(LogEventInfo logEvent)
        {
            Color textColor = Color.White;
            if (logEvent.Level == LogLevel.Warn)
            {
                textColor = Color.FromUint(0xFFFF00);
            }
            else if (logEvent.Level >= LogLevel.Error)
            {
                textColor = Color.FromUint(0xFF0000);
            }

            InformationManager.DisplayMessage(
                new InformationMessage(Layout.Render(logEvent), textColor));
        }
    }
}
