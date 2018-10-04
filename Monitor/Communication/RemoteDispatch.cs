using NetMQ.Sockets;

namespace Monitor
{
    public class RemoteDispatch
    {
        public RemoteDispatch(Message msg, string destination)
        {
            Msg = msg;
            Destination = destination;
        }

        public Message Msg { get; set; }
        public string Destination { get; set; }
    }
}