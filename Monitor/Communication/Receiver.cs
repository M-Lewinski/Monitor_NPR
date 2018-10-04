using System;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;

namespace Monitor.Communication
{
    public class Receiver : Messenger
    {
        public Receiver(RemoteServerGroup remoteServGr) : base(remoteServGr)
        {
        }

        public override Thread Start()
        {
            var receiveThread = new Thread(this.ContinousReceiveMessages);
            receiveThread.Start();
            return receiveThread;
        }

        public void ContinousReceiveMessages(){
            var pullSocket = new PullSocket();
            pullSocket.Bind("tcp://*:"+RemoteServGr.NodePort);
            while (RemoteServGr.Running)
            {
                var msgJson = pullSocket.ReceiveFrameString();
                ReceiveMessage(msgJson);
            }   
            var msg = new Message
            {
                MsgType = MessageType.EndingProgram,
                SendingServer = RemoteServGr.NodeAddress
            };
            var messageWrapper = new RemoteDispatch(msg,null);
            RemoteServGr.SendQueue.Add(messageWrapper);
            pullSocket.Close();
        }

        public void ReceiveMessage(string msgJson)
        {
            var msg = JsonConvert.DeserializeObject<Message>(msgJson);
            switch (msg.MsgType)
            {
                case MessageType.BarrierRequest:
                    ReceiveBarrierRequest(msg);
                    break;
                case MessageType.BarrierResponse:
                    ReceiveBarrierResponse(msg);
                    break;
                case MessageType.TokenRequest:
                    ReceiveTokenRequest(msg);
                    break;
                case MessageType.TokenRespond:
                    ReceivedTokenResponse(msg);
                    break;
                case MessageType.WaitRequest:
                    ReceiveWaitRequest(msg);
                    break;
                case MessageType.NotifyAllResponse:
                    ReceiveNotifyAllResponse(msg);
                    break;
                case MessageType.EndingProgram:
                    DecrementWorkingServersCount();
                    return;
            }
        }


        private void ReceiveTokenRequest(Message requestMsg)
        {
//            Console.WriteLine("Received Token Request");

            var sRequestNumber = int.Parse(requestMsg.Data);
            var monitor = RemoteServGr.Monitors[requestMsg.MonitorId];
            monitor.TokenMutex.WaitOne();
            if (sRequestNumber < monitor.RequestNumberList[requestMsg.SendingServer])
            {
                monitor.TokenMutex.ReleaseMutex();
                return;
            }

            monitor.RequestNumberList[requestMsg.SendingServer] = sRequestNumber;
            if (monitor.CurrentToken != null)
            {
                if (monitor.ExecutingCriticalSection == false)
                {
                    if (monitor.RequestNumberList[requestMsg.SendingServer] ==
                        monitor.CurrentToken.LastRequestNumberList[requestMsg.SendingServer] + 1)
                    {
                        monitor.SendToken(requestMsg.SendingServer);
                    }
                }
            }
            monitor.TokenMutex.ReleaseMutex();
        }

        private void ReceivedTokenResponse(Message requestMsg)
        {
            Console.WriteLine("Received Token Response");
            var token = JsonConvert.DeserializeObject<Token>(requestMsg.Data);
            var monitor = RemoteServGr.Monitors[requestMsg.MonitorId];
            monitor.TokenMutex.WaitOne();
            string monitorId = monitor.Id;
            JsonSerializerSettings populateSettings = new JsonSerializerSettings {ObjectCreationHandling = ObjectCreationHandling.Replace, TypeNameHandling = TypeNameHandling.All };
            JsonConvert.PopulateObject(token.ReceivedData,monitor,populateSettings);
            monitor.Id = monitorId;
            monitor.ReceivedToken.Add(token);
            monitor.TokenMutex.ReleaseMutex();

        }
        
        private void ReceiveBarrierRequest(Message requestMsg)
        {
            var response = new Message
            {
                MsgType = MessageType.BarrierResponse
            };
            var remoteDispatch = new RemoteDispatch(response,requestMsg.SendingServer);
            RemoteServGr.SendQueue.Add(remoteDispatch);
        }

        private void ReceiveBarrierResponse(Message reponseMsg)
        {
            RemoteServGr.SyncBarrier.Signal();
        }

        private void ReceiveWaitRequest(Message requestMsg)
        {
            var conditionalVariable = RemoteServGr.ConditionalVariables[requestMsg.Data];
            conditionalVariable.AllMonitorWait();
            
            if (!conditionalVariable.RemoteNodesWaitingQueue.Contains(requestMsg.SendingServer))
            {
                conditionalVariable.RemoteNodesWaitingQueue.Add(requestMsg.SendingServer);
            }
            conditionalVariable.AllMonitorRelease();
        }

        private void ReceiveNotifyAllResponse(Message responseMsg)
        {
            var conditionalVariable = RemoteServGr.ConditionalVariables[responseMsg.Data];
            conditionalVariable.AllMonitorWait();

            if (!conditionalVariable.RemoteNodesWaitingQueue.Contains(RemoteServGr.NodeAddress))
            {
                return;
            }

            conditionalVariable.RemoteNodesWaitingQueue.Remove(RemoteServGr.NodeAddress);
            foreach (var queue  in conditionalVariable.ConditionalQueues)
            {
                queue.Add(responseMsg.SendingServer);
            }
            conditionalVariable.ConditionalQueues.Clear();
            conditionalVariable.AllMonitorRelease();
        }

    }
}