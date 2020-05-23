namespace Sync
{
    public class BufferedData
    {
        public object Actual { get; set; } = null;
        public object ToSend { get; set; } = null;
        public bool Sent { get; set; } = false;
    }
}
