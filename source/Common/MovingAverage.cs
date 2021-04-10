using System;
using System.Linq;

namespace Common
{
    /// <summary>
    ///     Data structure to compute a moving average over a fixed number of values.
    /// </summary>
    public class MovingAverage
    {
        /// <summary>
        ///     The maximum number of values that are considered for the average.
        /// </summary>
        public int Size { get; }

        /// <summary>
        ///     Returns the smallest value in the data set.
        /// </summary>
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
        /// <summary>
        ///     Returns the highest value in the data set.
        /// </summary>
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
        /// <summary>
        ///     Returns the smallest value that was ever inserted into the data.
        /// </summary>
        public long AllTimeMin { get; private set; } = Int64.MaxValue;
        /// <summary>
        ///     Returns the highest value that was ever inserted into the data.
        /// </summary>
        public long AllTimeMax { get; private set; } = Int64.MinValue;
        /// <summary>
        ///     Constructs a new moving average.
        /// </summary>
        /// <param name="size">The number of data values kept to compute the average.</param>
        public MovingAverage(int size)
        {
            Size = size;
            m_Values = new long[size];
            m_iCount = 0;
            m_iFront = 0;
            m_iBack = 0;
            Average = 0;
        }
        /// <summary>
        ///     Returns the average value of the data.
        /// </summary>
        public double Average { get; private set; }
        /// <summary>
        ///     Pushes a new value into the data.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
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

        #region Private
        private int NextIndex(int index)
        {
            return (index + 1) % Size;
        }
        
        private readonly long[] m_Values;
        private int m_iBack;
        private int m_iCount;
        private int m_iFront;

        private long? m_Min;
        private long? m_Max;
        #endregion
    }
}
