
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using CommandLine;
using CSharpx;
using Monitor.Communication;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;

namespace Monitor
{
	public class RemoteServerGroup
	{
		private const string JsonRemoteAddressesList = "Remote";
		
		
		public int NodeAddressNumber { get; set; }
		public string NodeAddress { get; set; }
		public string NodePort { get; set; }
		
		[JsonProperty(JsonRemoteAddressesList)]
		public List<string> RemoteAddressesList { get; set; }
		
		public SortedDictionary<string,PushSocket> RemoteServerSockets { get; set; } = new SortedDictionary<string, PushSocket>();
		public SortedDictionary<string,MonitorBase> Monitors { get; set; } = new SortedDictionary<string, MonitorBase>();
		public SortedDictionary<string,ConditionalVariable> ConditionalVariables { get; set; } = new SortedDictionary<string, ConditionalVariable>();
		
		public BlockingCollection<RemoteDispatch> SendQueue { get; set; } = new BlockingCollection<RemoteDispatch>();
		
		public bool Running = true;
		
		public OptionsInit Init;

		public int WorkingRemoteServersCount;

		private Thread SenderThread { get; set; }
		private Thread ReceiverThread { get; set; }
		
		public CountdownEvent SyncBarrier { get; set; }

		private const int Timeout = 60;

		public bool MainNode { get; set; } = false;

		public static RemoteServerGroup Instance { get; set; }
		
		public static void Initiate(string[] args)
		{
			RemoteServerGroup.Initiate<OptionsInit>(args);
		}

		public static void Initiate<T>(string[] args) where T : OptionsInit
		{
			var result = Parser.Default.ParseArguments<T>(args).WithNotParsed(errors =>
			{
				foreach (var err in errors)
				{
					Console.WriteLine(err.ToString());
				}
				throw new Exception("Argument exception");
			});
			T init = ((Parsed<T>) result).Value;
			var remoteServerGroup = GetRemoteInfoFromFile(init.FileName);
			remoteServerGroup.Init = init;
			remoteServerGroup.NodeAddressNumber = init.Number;
			remoteServerGroup.CreateRemoteNode();
			Instance = remoteServerGroup;
		}
		
		private void CreateRemoteNode()
		{
			if (NodeAddressNumber >= RemoteAddressesList.Count)
				throw new Exception("Node number is not correct, ");
			NodeAddress = RemoteAddressesList[this.NodeAddressNumber];

			if (NodeAddressNumber == 0)
			{
				MainNode = true;
			}

			WorkingRemoteServersCount = RemoteAddressesList.Count;
			RemoteAddressesList.RemoveAt(NodeAddressNumber);


			NodePort = NodeAddress.Split(new[] {":"}, StringSplitOptions.None)[1];
			
			foreach (var remoteServer in RemoteAddressesList)
			{
				var remotePushSocket = new PushSocket();
				RemoteServerSockets[remoteServer] = remotePushSocket;
				remotePushSocket.Connect("tcp://"+remoteServer);
			}
		}


		private static RemoteServerGroup GetRemoteInfoFromFile(string fileName)
		{
			if (string.IsNullOrEmpty(fileName)) 
				throw new Exception("File name is null or empty");
			try
				{
					var jsonString = File.ReadAllText(fileName, Encoding.UTF8);
					var remoteServerGroup = JsonConvert.DeserializeObject<RemoteServerGroup>(jsonString);
					if (remoteServerGroup.RemoteAddressesList == null ||
						remoteServerGroup.RemoteAddressesList.Count == 0)
						throw new Exception("Incorrect configuration file");
					return remoteServerGroup;
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
					throw;
				}
		}

		public static void StartRemoteExecution()
		{
			Instance.PrepareRemoteConnection();
		}

		private void PrepareRemoteConnection()
		{
			SyncBarrier = new CountdownEvent(this.RemoteAddressesList.Count);
			
			ReceiverThread = new Receiver(this).Start();
			SenderThread = new Sender(this).Start();
			
			BarrierWithTimeout();
			Console.WriteLine("Finished starting remote server");
		}
		

		private void BarrierWithTimeout()
		{
			var msg = new Message
			{
				MsgType = MessageType.BarrierResponse,
				SendingServer = NodeAddress,
			};
			foreach (var remoteAddress in RemoteAddressesList)
			{
				var respond = new RemoteDispatch(msg,remoteAddress);
				SendQueue.Add(respond);
			}
			
			Console.WriteLine("Waiting on barrier");
			if (!SyncBarrier.Wait(TimeSpan.FromSeconds(Timeout)))
			{
				throw new Exception("Barrier timeout");
			}
		}

		private void InformSenderAboutFinish()
		{
			var msg = new Message
			{
				MsgType = MessageType.EndingProgram,
				SendingServer = this.NodeAddress
			};
			var messageWrapper = new RemoteDispatch(msg,null);
			SendQueue.Add(messageWrapper);
		}

		public static void Finish()
		{
			var rsg = Instance;
			rsg.InformSenderAboutFinish();
			rsg.SenderThread.Join();
			rsg.ReceiverThread.Join();
		}
	}
}