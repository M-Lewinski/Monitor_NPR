using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Monitor
{
    public class ConditionalVariable
    {
        public string Id;
        public List<MonitorBase> AssignedMonitors { get; set; }
        
        public List<BlockingCollection<string>> ConditionalQueues { get; set; }
        
        public List<string> RemoteNodesWaitingQueue { get; set; }

        private ConditionalVariable(string id, params MonitorBase[] assignedMonitors)
        {
            Id = id;
            AssignedMonitors = new List<MonitorBase>();
            ConditionalQueues = new List<BlockingCollection<string>>();
            RemoteNodesWaitingQueue = new List<string>();
            var rsg = RemoteServerGroup.Instance;
            rsg.ConditionalVariables[Id] = this;
            
            foreach (var monitor in assignedMonitors)
            {
                AssignedMonitors.Add(monitor);
            }
        }

        public void Wait()
        {
            var waitForSignal = new BlockingCollection<string>();
            ConditionalQueues.Add(waitForSignal);
            var remoteServerGroup = RemoteServerGroup.Instance;
            if (!RemoteNodesWaitingQueue.Contains(remoteServerGroup.NodeAddress))
            {
                RemoteNodesWaitingQueue.Add(remoteServerGroup.NodeAddress);
            }
            var msg = new Message
            {
                MsgType = MessageType.WaitRequest,
                Data = Id,
                SendingServer = remoteServerGroup.NodeAddress,
            };

            foreach (var remoteAddress in remoteServerGroup.RemoteAddressesList)
            {
                var remoteDispatch = new RemoteDispatch(msg,remoteAddress);
                remoteServerGroup.SendQueue.Add(remoteDispatch);
            }

            for (var i = AssignedMonitors.Count - 1; i >= 0; i--)
            {
                AssignedMonitors[i].ReleaseLock();
            }
            var notifiedBy = waitForSignal.Take();
            Console.WriteLine($"Notified by {notifiedBy}");
            for (var i = 0; i < AssignedMonitors.Count; i++)
            {
                AssignedMonitors[i].AcquireLock();
            }
        }

        public void NotifyAll()
        {    
            var msg = new Message
            {
                MsgType = MessageType.NotifyAllResponse,
                Data = Id,
                SendingServer = RemoteServerGroup.Instance.NodeAddress
            };
            while (RemoteNodesWaitingQueue.Count > 0)
            {
                if (RemoteNodesWaitingQueue[0] == RemoteServerGroup.Instance.NodeAddress)
                {
                    foreach (var queue in ConditionalQueues)
                    {
                        queue.Add(RemoteServerGroup.Instance.NodeAddress);
                    }

                    ConditionalQueues.Clear();
                }
                else
                {
                    var remoteAddress = RemoteNodesWaitingQueue[0];
                    var remoteDispatch = new RemoteDispatch(msg,remoteAddress);
                    RemoteServerGroup.Instance.SendQueue.Add(remoteDispatch);
                }
                RemoteNodesWaitingQueue.RemoveAt(0);
            }
        }

        public void AllMonitorRelease()
        {
            foreach (var monitor in AssignedMonitors)
                monitor.LocalMutex.ReleaseMutex();
        }

        public void AllMonitorWait()
        {
            foreach (var monitor in AssignedMonitors)
            {
                monitor.LocalMutex.WaitOne();
            }
        }
    }
}