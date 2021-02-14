using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.Party;
using NLog;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;
using RailgunNet.Util;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.DebugUtil
{
    /// <summary>
    ///     Module for search abnormal desynchronization between two identical movement sequences.
    ///     There are three steps:
    ///     <list type="table">
    ///         <item>(1) record movements</item>
    ///         <item>(2) playback movements</item>
    ///         <item>(3) verify sync of movements</item>
    ///     </list>
    /// </summary>
    /// <remarks>
    ///     See
    ///     <a href="https://github.com/Bannerlord-Coop-Team/BannerlordCoop/wiki">BannerlordCoop wiki</a>
    /// </remarks>
    public static class Replay
    {
        public enum ReplayState
        {
            Stop,
            Recording,
            Playback
        }

        private const string logsDir = "logs";
        private const string recordExt = ".replay";
        private const string logFilename = "-verified.html";
        private const int averageEventsCountInColumn = 5;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static string currentFilename;

        // 
        private static CampaignTime firstTick;
        private static CampaignTime lastTick;
        private static List<ReplayEvent> RecordingEventList;

        private static List<ReplayEvent> PlaybackEventList;

        // list of main party movement which we play manually while playback
        private static List<ReplayEvent> PlaybackMainPartyList;

        private static ReplayState state { get; set; } = ReplayState.Stop;

        // point of recording movements; happen on client side
        // TODO: maybe remove recording point onto server side?
        public static Action<EntityId, MobileParty, MovementData> ReplayRecording
        {
            get;
            private set;
        }

        // point of playback recorded movements; happen on server side
        // TODO: maybe remove playback point onto client side? or send events data to server through network?
        public static Action ReplayPlayback { get; private set; }

        private static bool isValid(string fileName)
        {
            return !string.IsNullOrEmpty(fileName) &&
                   fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        /// <summary>
        ///     Start recording movement and wait 'stop' command.
        ///     First step.
        /// </summary>
        /// <param name="filename">Filename without path and extension</param>
        /// <returns></returns>
        internal static string StartRecord(string filename)
        {
            if (state == ReplayState.Playback)
            {
                return "Could not start recording while playback.";
            }

            if (CoopServer.Instance?.Persistence?.Room?.Tick.IsValid != true)
            {
                return "Could not start recording while server not started.";
            }

            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }

            if (!isValid(filename))
            {
                return $"Filename '{filename}' is not valid file name.";
            }

            string path = Path.Combine(logsDir, filename + recordExt);
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception)
                {
                    return $"Could not delete existed file '{path}'";
                }
            }

            RecordingEventList = new List<ReplayEvent>();

            currentFilename = filename;
            state = ReplayState.Recording;
            ReplayRecording += OnEventRecording;

            return $"Recording '{filename}' started.";
        }

        /// <summary>
        ///     Start replay movements of main party from file and wait 'stop' command.
        ///     At the same time it starting recording movement for further analize.
        ///     Second step.
        /// </summary>
        /// <param name="filename">Filename without path and extension</param>
        /// <returns></returns>
        internal static string Playback(string filename)
        {
            if (state == ReplayState.Recording)
            {
                return "Could not start playback while recording.";
            }

            if (CoopServer.Instance?.Persistence?.Room?.Tick.IsValid != true)
            {
                return "Could not start playback while server not started.";
            }

            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }

            if (!isValid(filename))
            {
                return $"Filename '{filename}' is not valid file name.";
            }

            string path = Path.Combine(logsDir, filename + recordExt);
            if (!File.Exists(path))
            {
                return $"Could not find file '{path}'";
            }

            RailBitBuffer buffer = new RailBitBuffer();
            buffer.Load(new ArraySegment<byte>(File.ReadAllBytes(path)));
            PlaybackEventList = new List<ReplayEvent>();
            while (!buffer.IsFinished)
            {
                PlaybackEventList.AddRange(buffer.UnpackAll(q => q.ReadReplayEvent()));
            }

            if (PlaybackEventList.Count == 0)
            {
                return "Record is empty.";
            }

            firstTick = PlaybackEventList.First().time;
            lastTick = PlaybackEventList.Last().time;

            CampaignTime now = CampaignTime.Now;
            if (now > firstTick)
            {
                return $"Tick : {(long) now.ToMilliseconds} > {(long) firstTick.ToMilliseconds}\n" +
                       "Current campaign time is passed.";
            }

            PlaybackMainPartyList =
                PlaybackEventList.Where(q => q.party.IsAnyPlayerMainParty()).ToList();
            RecordingEventList = new List<ReplayEvent>();

            currentFilename = filename;
            state = ReplayState.Playback;
            ReplayPlayback += OnEventPlayback;

            // TODO
            // CoopServer.Instance.Persistence.EntityManager.WorldEntityServer.State.TimeControl =
            //     CampaignTimeControlMode.UnstoppablePlay;
            // CoopServer.Instance.Persistence.EntityManager.WorldEntityServer.State.TimeControlLock =
            //     false;

            return $"Playback file '{filename}' started.";
        }

        /// <summary>
        ///     Stop recording and save it in file if it was recording state.
        ///     Stop playback and verify first recorded movements with second recorded movements.
        /// </summary>
        /// <returns></returns>
        internal static string Stop()
        {
            switch (state)
            {
                case ReplayState.Stop:
                    return "Nothing happend.";

                case ReplayState.Recording:
                    ReplayRecording -= OnEventRecording;
                    state = ReplayState.Stop;

                    RailBitBuffer buffer = new RailBitBuffer();
                    int count = RecordingEventList.Count / RailBitBuffer.MAX_LIST_COUNT;
                    for (int i = 0; i < count; i++)
                    {
                        buffer.PackAll(
                            RecordingEventList.GetRange(
                                i * RailBitBuffer.MAX_LIST_COUNT,
                                RailBitBuffer.MAX_LIST_COUNT),
                            q => buffer.WriteReplayEvent(q));
                    }

                    buffer.PackAll(
                        RecordingEventList.GetRange(
                            count * RailBitBuffer.MAX_LIST_COUNT,
                            RecordingEventList.Count % RailBitBuffer.MAX_LIST_COUNT),
                        q => buffer.WriteReplayEvent(q));

                    byte[] data = new byte[buffer.ByteSize + 4];
                    Array.Resize(ref data, buffer.Store(data));

                    string path = Path.Combine(logsDir, currentFilename + recordExt);
                    File.WriteAllBytes(path, data);

                    RecordingEventList = null;
                    return $"Recording stopped and exported in file '{currentFilename}'.";

                case ReplayState.Playback:
                    ReplayPlayback -= OnEventPlayback;
                    ReplayRecording -= OnEventRecording;
                    state = ReplayState.Stop;

                    VerifyEvents();

                    RecordingEventList = null;
                    PlaybackEventList = null;
                    PlaybackMainPartyList = null;
                    return $"Playback file '{currentFilename}' stopped.";

                default:
                    return null;
            }
        }

        /// <summary>
        ///     Record party movement while first and second steps
        /// </summary>
        /// <param name="entityId"></param>
        /// <param name="party"></param>
        /// <param name="movement"></param>
        private static void OnEventRecording(
            EntityId entityId,
            MobileParty party,
            MovementData movement)
        {
            RecordingEventList.Add(
                new ReplayEvent
                {
                    time = CampaignTime.Now,
                    entityId = entityId,
                    party = party,
                    movement = movement
                });
            if (party.IsAnyPlayerMainParty())
            {
                Logger.Info("[REPLAY] Yet one player's moving recorded.");
            }
        }

        /// <summary>
        ///     Replay main party movement
        /// </summary>
        private static void OnEventPlayback()
        {
            CampaignTime now = CampaignTime.Now;

            // start recording for second step
            if (firstTick <= now)
            {
                ReplayRecording += OnEventRecording;
                firstTick = CampaignTime.Never;
            }
            else
            {
                return;
            }

            ReplayEvent replay =
                PlaybackMainPartyList.FirstOrDefault(q => !q.applied && q.time <= now);
            if (replay != null)
            {
                if (CoopServer.Instance.Persistence.Room.Entities.FirstOrDefault(
                    q => q.Id == replay.entityId) is MobilePartyEntityServer entity)
                {
                    entity.State.Movement = new MovementState
                    {
                        DefaultBehavior = replay.movement.DefaultBehaviour,
                        Position = replay.movement.TargetPosition,
                        SettlementIndex = replay.movement.TargetSettlement != null ?
                            replay.movement.TargetSettlement.Id :
                            MovementState.InvalidIndex,
                        TargetPartyIndex = replay.movement.TargetParty != null ?
                            replay.movement.TargetParty.Id :
                            MovementState.InvalidIndex
                    };
                }

                replay.applied = true;
                Logger.Info("[REPLAY] Moving to new position.");
            }

            // stop recording end playback when all recorded movements was replayed
            if (lastTick <= now)
            {
                Stop();
                // TODO: send message in debug console instead to send on game screen
                Logger.Info("[REPLAY] Playback has finished.");
            }
        }

        /// <summary>
        ///     Verify difference in start time of similar movements in first recorded data (recording state)
        ///     and last recorded data (playback state).
        ///     Third step.
        /// </summary>
        private static void VerifyEvents()
        {
            List<long?> diffTime = new List<long?>();

            foreach (ReplayEvent play in PlaybackEventList)
            {
                ReplayEvent rec =
                    RecordingEventList.FirstOrDefault(q => !q.applied && q.Equals(play));
                if (rec != null)
                {
                    // rec.time - CampaignTime of movement recorded in playback state (second step)
                    // play.time - CampaignTime of movement recorded in recording state (first step)
                    diffTime.Add((long) (rec.time - play.time).ToMilliseconds);
                    rec.applied = true;
                }
                else
                {
                    diffTime.Add(null);
                }
            }

            int skipped = diffTime.Count(q => q == null);
            int newEvents = RecordingEventList.Count(q => !q.applied);
            if (skipped > 0)
            {
                Logger.Info($"[REPLAY] Skipped {skipped} movements.");
            }

            if (newEvents > 0)
            {
                Logger.Info($"[REPLAY] There is {newEvents} new movements.");
            }

            string path = Path.Combine(logsDir, currentFilename + logFilename);
            ExportToFile(path, diffTime, skipped, newEvents);

            Logger.Info($"[REPLAY] Verifying has finished and exported into '{path}'.");
        }

        /// <summary>
        ///     Export verified data in html-report (compatible with Excel).
        /// </summary>
        /// <param name="path">path with filename and extension where report will be stored</param>
        /// <param name="diffTime">
        ///     data with difference in start time of movements (in milliseconds of in-game
        ///     time)
        /// </param>
        /// <param name="skipped">count of unknown movements in first recorded data (recording state)</param>
        /// <param name="newEvents">count of unknown movements in second recorded data (playback state)</param>
        private static void ExportToFile(
            string path,
            List<long?> diffTime,
            int skipped,
            int newEvents)
        {
            List<object[]> table = diffTime.Where(q => q != null)
                                           .Select(
                                               q => new object[6] {q, 0, null, null, null, null})
                                           .ToList();

            int l_minValue = (int) diffTime.Min().Value;
            int l_maxValue = (int) diffTime.Max().Value;

            int minValue = Math.Sign(l_minValue) * RailUtil.Log2((ulong) Math.Abs(l_minValue));
            int maxValue = Math.Sign(l_maxValue) * RailUtil.Log2((ulong) Math.Abs(l_maxValue)) + 1;

            int colsCount = table.Count / averageEventsCountInColumn;
            int colWidth = (maxValue - minValue) / colsCount;

            table[0][2] = skipped;
            table[0][3] = newEvents;
            table[0][4] = l_minValue;
            table[0][5] = l_maxValue;

            foreach (object[] line in table)
            {
                long diff = ((long?) line[0]).Value;
                int index = (Math.Sign(diff) * RailUtil.Log2((ulong) Math.Abs(diff)) - minValue) /
                            colWidth;
                table[index][1] = (int) table[index][1] + 1;
            }

            string report = $@"<html xmlns:o='urn:schemas-microsoft-com:office:office'
    xmlns:x='urn:schemas-microsoft-com:office:excel'
    xmlns='http://www.w3.org/TR/REC-html40'>
    <head>
        <xml>
            <x:ExcelWorkbook>
                <x:ExcelWorksheets>
                    <x:ExcelWorksheet>
                        <x:Name>{firstTick}</x:Name>
                        <x:WorksheetOptions>
                            <x:Print>
                                <x:Gridlines />
                            </x:Print>
                        </x:WorksheetOptions>
                    </x:ExcelWorksheet>
                </x:ExcelWorksheets>
            </x:ExcelWorkbook>
        </xml>
    </head>                      
    <body>
        <table>
            <tr>
                <th>timeDiff(ms)</th>
                <th>cols</th>
                <th>skipped</th>
                <th>newEvents</th>
                <th>minValue</th>
                <th>maxValue</th>
            </tr>
";

            foreach (object[] line in table)
            {
                report +=
                    $"<tr><td>{line[0]}</td><td>{line[1]}</td><td>{line[2]}</td><td>{line[3]}</td>" +
                    $"<td>{line[4]}</td><td>{line[5]}</td>\r\n";
            }

            report += "</table></body></html>";

            File.WriteAllText(path, report);
        }
    }
}
