namespace Monitor
{
    public class Message
    {
        public MessageType MsgType { get; set; }
        public string MonitorId { get; set; }
        public string Data { get; set; }
        public string SendingServer { get; set; }
    }
}