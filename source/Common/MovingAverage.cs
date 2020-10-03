using System;
using System.Linq;

namespace Common
{
    public class MovingAverage
    {
        public int Size { get; }

        private readonly long[] m_Values;
        private int m_iBack;
        private int m_iCount;
        private int m_iFront;

        private long? m_Min;
        private long? m_Max;

        public long Min
        {
            get
            {
                if (!m_Min.HasValue)
                {
                    m_Min = m_Values.Min();
                }
                return m_Min.Value;
            }
        }
        public long Max
        {
            get
            {
                if (!m_Max.HasValue)
                {
                    m_Max = m_Values.Max();
                }
                return m_Max.Value;
            }
        }
        
        public long AllTimeMin { get; private set; } = Int64.MaxValue;
        public long AllTimeMax { get; private set; } = Int64.MinValue;

        public MovingAverage(int size)
        {
            Size = size;
            m_Values = new long[size];
            m_iCount = 0;
            m_iFront = 0;
            m_iBack = 0;
            Average = 0;
        }

        public double Average { get; private set; }

        public double Push(long value)
        {
            if (m_iCount == Size)
            {
                Average = (Average * Size - m_Values[m_iBack] + value) / Size;
                m_Values[m_iBack] = value;
                m_iFront = NextIndex(m_iFront);
                m_iBack = NextIndex(m_iBack);
            }
            else
            {
                Average = (Average * m_iCount + value) / ++m_iCount;
                m_Values[m_iBack] = value;
                m_iBack = NextIndex(m_iBack);
            }

            m_Min = null;
            m_Max = null;
            
            AllTimeMin = Math.Min(AllTimeMin, value);
            AllTimeMax = Math.Max(AllTimeMax, value);
            
            return Average;
        }

        private int NextIndex(int index)
        {
            return (index + 1) % Size;
        }
    }
}
