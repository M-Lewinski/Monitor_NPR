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

        public Mutex ConditionMutex { get; set; }

        public void Wait()
        {
            ConditionMutex.WaitOne();
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
            ConditionMutex.ReleaseMutex();
            Console.WriteLine("Waiting to be notified");
            var notifiedBy = waitForSignal.Take();
            Console.WriteLine($"Notified by {notifiedBy}");
            for (var i = 0; i < AssignedMonitors.Count; i++)
            {
                AssignedMonitors[i].AcquireLock();
            }
        }

        public ConditionalVariable(string id, params MonitorBase[] assignedMonitors)
        {
            ConditionMutex = new Mutex();
            Id = id;
            AssignedMonitors = new List<MonitorBase>();
            ConditionalQueues = new List<BlockingCollection<string>>();
            RemoteNodesWaitingQueue = new List<string>();
            var rsg = RemoteServerGroup.Instance;
            if (rsg.ConditionalVariables.ContainsKey(Id))
            {
                throw new Exception($"Conditional variable with id : {Id} already exists");
            }
            rsg.ConditionalVariables[Id] = this;
            
            foreach (var monitor in assignedMonitors)
            {
                AssignedMonitors.Add(monitor);
            }
        }

        public void NotifyAll()
        {
            ConditionMutex.WaitOne();
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
            ConditionMutex.ReleaseMutex();
        }

    }
}