using System;
using System.Collections.Generic;
using System.Threading;
using Monitor;
using Newtonsoft.Json;

namespace ProducerConsumer
{
    
    public class BufforMonitor : MonitorBase
    {
        public List<int> Buffor { get; set; }

        public int BufforMaxSize { get; set; }

        public int LastCreatedValue { get; set; }
        
        [JsonIgnore]
        public int LocalValue { get; set; }


        public BufforMonitor(int bufforMaxSize,int localValue)
        {
            Buffor = new List<int>();
            LastCreatedValue = 0;
            BufforMaxSize = bufforMaxSize;
            LocalValue = localValue;
        }
    }

    public class Consumer
    {
        public BufforMonitor SharedBufforMonitor { get; set; }
        public ConditionalVariable Empty;
        public ConditionalVariable Full;


        public Consumer(ConditionalVariable empty, ConditionalVariable full, BufforMonitor sharedBufforMonitor)
        {
            Empty = empty;
            Full = full;
            SharedBufforMonitor = sharedBufforMonitor;
        }

        public void Consume()
        {
            SharedBufforMonitor.AcquireLock();
            while (SharedBufforMonitor.Buffor.Count == 0)
            {
                Empty.Wait();                
            }

            var value = SharedBufforMonitor.Buffor[0];
            Console.WriteLine($"Consumed: {value}");
            Full.NotifyAll();
            SharedBufforMonitor.ReleaseLock();
        }
    }
    
    public class Producer
    {
        public BufforMonitor SharedBufforMonitor { get; set; }
        public ConditionalVariable Empty;
        public ConditionalVariable Full;


        public Producer(ConditionalVariable empty, ConditionalVariable full, BufforMonitor sharedBufforMonitor)
        {
            Empty = empty;
            Full = full;
            SharedBufforMonitor = sharedBufforMonitor;
        }

        public void Produce()
        {
            SharedBufforMonitor.AcquireLock();
            while (SharedBufforMonitor.Buffor.Count == SharedBufforMonitor.BufforMaxSize)
            {
                Full.Wait();                
            }

            var value = SharedBufforMonitor.LastCreatedValue++;
            Console.WriteLine($"Produced: {value}");
            SharedBufforMonitor.Buffor.Add(value);
            Empty.NotifyAll();
            SharedBufforMonitor.ReleaseLock();
        }
    }


    class Program
    {
        
        static void Main(string[] args)
        {
            RemoteServerGroup.Initiate(args);
            
            BufforMonitor newBufforMonitor = new BufforMonitor(3,RemoteServerGroup.Instance.NodeAddressNumber);
            RemoteServerGroup.StartRemoteExecution();
            Console.WriteLine($"Before: {newBufforMonitor.Id} 0 {newBufforMonitor.LocalValue}");
            newBufforMonitor.AcquireLock();
            if (RemoteServerGroup.Instance.NodeAddressNumber > 0)
            {
                while (newBufforMonitor.Buffor.Count == 0)
                {
                    newBufforMonitor.ReleaseLock();
                    Thread.Sleep(1000);
                    newBufforMonitor.AcquireLock();
                }
                Console.WriteLine($"result {newBufforMonitor.Buffor[0]}");
            }
            else
            {
                newBufforMonitor.Buffor.Add(new Random().Next());
            }
            newBufforMonitor.ReleaseLock();
            Console.WriteLine($"After: {newBufforMonitor.Id} {newBufforMonitor.LocalValue}");
            RemoteServerGroup.Finish();
        }
        
    }
}