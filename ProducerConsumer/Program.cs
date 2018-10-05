using System;
using System.Collections.Generic;
using System.Threading;
using Monitor;
using Newtonsoft.Json;
using ProducerConsumer;

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
            while (true)
            {
                SharedBufforMonitor.AcquireLock();
                while (SharedBufforMonitor.Buffor.Count == 0)
                {
                    Empty.Wait();
                }
                var value = SharedBufforMonitor.Buffor[0];
                Console.WriteLine($"$$$$ Consumed: {value} $$$$");
                SharedBufforMonitor.Buffor.RemoveAt(0);
                Full.NotifyAll();
                SharedBufforMonitor.ReleaseLock();
                
                Console.WriteLine(".... Consumer working ....");
                Thread.Sleep(3000*new Random().Next(5));
            }
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
            while (true)
            {

                SharedBufforMonitor.AcquireLock();
                while (SharedBufforMonitor.Buffor.Count == SharedBufforMonitor.BufforMaxSize)
                {
                    Full.Wait();
                }
                var value = SharedBufforMonitor.LastCreatedValue++;
                Console.WriteLine($"$$$$ Produced: {value} $$$$");
                SharedBufforMonitor.Buffor.Add(value);
                Empty.NotifyAll();
                SharedBufforMonitor.ReleaseLock();
                Console.WriteLine(".... Producer working ....");
                Thread.Sleep(3000*new Random().Next(5));
            }
        }
    }


    class Program
    {

        static void Main(string[] args)
        {
            RemoteServerGroup.Initiate(args); // Inicjalizacja singletona Remote Server Group
            
            // Tutaj powinny zostać stworzone wszystkie Monitory i zmienne warunkowe
            
            BufforMonitor sharedBufforMonitor = new BufforMonitor(3,RemoteServerGroup.Instance.NodeAddressNumber);
            
            ConditionalVariable empty = new ConditionalVariable("Empty",sharedBufforMonitor);
            ConditionalVariable full = new ConditionalVariable("full",sharedBufforMonitor);
            
            RemoteServerGroup.StartRemoteExecution(); // Barriera zapewniająca, że wszystkie servery wykonają poprawnie całą inicjalizację obiektów

            if (RemoteServerGroup.Instance.NodeAddressNumber < 2)
            {
                Consumer consumer = new Consumer(empty,full,sharedBufforMonitor);
                consumer.Consume();
            }
            else
            {
                Producer producer = new Producer(empty,full,sharedBufforMonitor);
                producer.Produce();
            }
            RemoteServerGroup.Finish();
        }
        
    }
}