using System;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;

namespace Monitor.Communication
{
    public class Sender : Messenger
    {
        public Sender(RemoteServerGroup remoteServGr) : base(remoteServGr)
        {
        }

        public override Thread Start()
        {
            var sendThread = new Thread(ContinousSendMessages);
            sendThread.Start();
            return sendThread;
        }

        
        public void ContinousSendMessages()
        {
            while (RemoteServGr.Running)
            {
                var messageToSend = RemoteServGr.SendQueue.Take();
                if (messageToSend.Msg.MsgType == MessageType.EndingProgram)
                {
                    DecrementWorkingServersCount();
                    foreach (var remoteAddress in RemoteServGr.RemoteAddressesList)
                    {
                        messageToSend.Destination = remoteAddress;
                        SendMessage(messageToSend);
                    }
                    continue;
                }
                SendMessage(messageToSend);
            }

            foreach (var sock in RemoteServGr.RemoteServerSockets)
            {
                sock.Value.Close();
            }
            CloseSendingSockets();
        }

        
        public void CloseSendingSockets()
        {
            Console.WriteLine("FINISHED SENDING DATA");
            var selfSocket = new PushSocket();
            selfSocket.Connect("tcp://"+RemoteServGr.NodeAddress);
            var msg = new Message
            {
                MsgType = MessageType.EndingProgram,
                SendingServer = RemoteServGr.NodeAddress
            };
            var msgJson = JsonConvert.SerializeObject(msg);
            selfSocket.SendFrame(msgJson);

            foreach (var socket in RemoteServGr.RemoteServerSockets)
            {
                socket.Value.Close();
            }
            selfSocket.Close();

        }

        public void SendMessage(RemoteDispatch remoteDispatch)
        {
            var msgJson = JsonConvert.SerializeObject(remoteDispatch.Msg);
            var socket = RemoteServGr.RemoteServerSockets[remoteDispatch.Destination];
            socket.SendFrame(msgJson);
        }

    }
}