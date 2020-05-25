namespace Sync
{
    public class BufferedData
    {
        public object Actual { get; set; }
        public object ToSend { get; set; }
        public bool Sent { get; set; }
    }
}
