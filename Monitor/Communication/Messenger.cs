using System;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;

namespace Monitor.Communication
{
    public abstract class Messenger
    {
        protected RemoteServerGroup RemoteServGr { get; set; }

        protected Messenger(RemoteServerGroup remoteServGr)
        {
            RemoteServGr = remoteServGr;
        }

        public abstract Thread Start();
        

        public void DecrementWorkingServersCount()
        {
            if (Interlocked.Decrement(ref RemoteServGr.WorkingRemoteServersCount) <= 0)
            {
                RemoteServGr.Running = false;

            }
        }

    }
    
}