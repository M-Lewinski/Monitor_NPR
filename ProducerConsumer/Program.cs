using System;
using Monitor;

namespace ProducerConsumer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
	        RemoteInfo.Initiate(args);
        }
    }
}