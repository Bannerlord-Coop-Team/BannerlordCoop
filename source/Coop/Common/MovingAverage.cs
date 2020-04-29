namespace Coop.Common
{
    public class MovingAverage
    {
        private readonly int m_iSize;

        private readonly long[] m_Values;
        private int m_iBack;
        private int m_iCount;
        private int m_iFront;

        public MovingAverage(int iSize)
        {
            m_iSize = iSize;
            m_Values = new long[iSize];
            m_iCount = 0;
            m_iFront = 0;
            m_iBack = 0;
            Average = 0;
        }

        public double Average { get; private set; }

        public double Push(long value)
        {
            if (m_iCount == m_iSize)
            {
                Average = (Average * m_iSize - m_Values[m_iBack] + value) / m_iSize;
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

            return Average;
        }

        private int NextIndex(int index)
        {
            return (index + 1) % m_iSize;
        }
    }
}
