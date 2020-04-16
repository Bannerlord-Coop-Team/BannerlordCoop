using System;
using System.Diagnostics;
using System.Threading;

namespace Coop.Common
{
    class FrameLimiter
    {
        private readonly Action<long> m_WaitUntilTick;
        private readonly long m_TargetTicksPerFrame;
        private readonly Stopwatch m_Timer;
        private readonly MovingAverage m_Avg;
        public TimeSpan LastFrameTime;
        public FrameLimiter(TimeSpan targetFrameTime)
        {
            m_TargetTicksPerFrame = targetFrameTime.Ticks;
            m_Timer = Stopwatch.StartNew();
            if (targetFrameTime == TimeSpan.Zero)
            {
                m_WaitUntilTick = tick => { };
            }
            else if (targetFrameTime > TimeSpan.FromMilliseconds(1))
            {
                m_WaitUntilTick = tick =>
                {
                    TimeSpan waitTime = TimeSpan.FromTicks(tick) - m_Timer.Elapsed;
                    if (waitTime > TimeSpan.Zero)
                    {
                        Thread.Sleep(waitTime);
                    }
                };
            }
            else
            {
                m_WaitUntilTick = tick =>
                {
                    SpinWait.SpinUntil(() => { return m_Timer.ElapsedTicks >= tick; });
                };
            }
            m_Avg = new MovingAverage(32);
        }

        public void Throttle()
        {
            long elapsedTicks = m_Timer.Elapsed.Ticks;
            var avgTicksPerFrame = m_Avg.Push(elapsedTicks);
            if (avgTicksPerFrame < m_TargetTicksPerFrame)
            {
                m_WaitUntilTick(elapsedTicks + (m_TargetTicksPerFrame - (long)avgTicksPerFrame));
            }
            LastFrameTime = m_Timer.Elapsed;
            m_Timer.Restart();
        }
    }
}
