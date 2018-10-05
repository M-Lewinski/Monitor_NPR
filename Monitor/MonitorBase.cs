using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using NetMQ;
using Newtonsoft.Json;

namespace Monitor
{
	public class MonitorBase
	{
		
		[JsonIgnore]
		public string Id;
		
		[JsonIgnore]
		public Mutex LocalMutex { get; set; }
		
		[JsonIgnore]
		public Mutex TokenMutex { get; set; }

		[JsonIgnore]
		public SortedDictionary<string,long> RequestNumberList { get; set; }

		[JsonIgnore]
		public bool ExecutingCriticalSection { get; set; }

		[JsonIgnore] public BlockingCollection<Token> ReceivedToken { get; set; }

		[JsonIgnore]
		public Token CurrentToken { get; set; }

		[JsonConstructor]
		public MonitorBase(bool test)
		{
			
		}
		
		public MonitorBase() : this(null)
		{
			Console.WriteLine("Parametless constructor");
		}
		
		public MonitorBase(string id)
		{
			LocalMutex = new Mutex();
			TokenMutex = new Mutex();
			var rsg = RemoteServerGroup.Instance;
			RequestNumberList = new SortedDictionary<string, long>();
			ReceivedToken = new BlockingCollection<Token>(1);
			ExecutingCriticalSection = false;
			Id = id;
			if (id == null)
			{
				Id = this.GetType().ToString();
			}
			RequestNumberList[rsg.NodeAddress] = 0;
			foreach (var remoteNode in rsg.RemoteAddressesList)
			{
				RequestNumberList[remoteNode] = 0;
			}
			
			if (rsg.MainNode)
			{
				CurrentToken = new Token(rsg);
			}

			if (rsg.Monitors.ContainsKey(Id))
			{
				throw new Exception($"Monitor with id: {Id} already exists");
			}
			rsg.Monitors[Id] = this;
		}

		public void AcquireLock()
		{
			LocalMutex.WaitOne();
			Console.WriteLine("Acquire Lock");
			RequestToken();
			ExecutingCriticalSection = true;
			TokenMutex.ReleaseMutex();
		}

		public void ReleaseLock()
		{
			ReleaseToken();
			LocalMutex.ReleaseMutex();
		}

		public void RequestToken()
		{
			TokenMutex.WaitOne();
			if (CurrentToken == null)
			{
				Console.WriteLine("Requesting Token");
				RequestNumberList[RemoteServerGroup.Instance.NodeAddress]++;
				while (CurrentToken == null)
				{
					var rsg = RemoteServerGroup.Instance;
					var msg = new Message
					{
						Data = RequestNumberList[RemoteServerGroup.Instance.NodeAddress].ToString(),
						MonitorId = Id,
						MsgType = MessageType.TokenRequest,
						SendingServer = rsg.NodeAddress
					};

					foreach (var remoteAddress in rsg.RemoteAddressesList)
					{
						var remoteDispatch = new RemoteDispatch(msg, remoteAddress);
						rsg.SendQueue.Add(remoteDispatch);
					}

					TokenMutex.ReleaseMutex();
					CurrentToken = ReceivedToken.Take();
					TokenMutex.WaitOne();
				}
			}
		}

		public void ReleaseToken()
		{
			TokenMutex.WaitOne();
			Console.WriteLine("Releasing Token");
			CurrentToken.LastRequestNumberList[RemoteServerGroup.Instance.NodeAddress] = RequestNumberList[RemoteServerGroup.Instance.NodeAddress];
			var rsg = RemoteServerGroup.Instance;
			foreach (var remoteAddress in rsg.RemoteAddressesList)
			{
				if (!CurrentToken.TokenWaitingQueueu.Contains(remoteAddress))
				{
					if(RequestNumberList[remoteAddress] == CurrentToken.LastRequestNumberList[remoteAddress]+1)
					CurrentToken.TokenWaitingQueueu.Add(remoteAddress);
				}
			}

			if (CurrentToken.TokenWaitingQueueu.Count > 0)
			{
				var destination = CurrentToken.TokenWaitingQueueu[0];
				CurrentToken.TokenWaitingQueueu.RemoveAt(0);
				SendToken(destination);
			}
			
			ExecutingCriticalSection = false;
			TokenMutex.ReleaseMutex();
		}

		public void SendToken(string destination)
		{
			Console.WriteLine($"Sending Token to: {destination}");
			JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

			CurrentToken.ReceivedData = JsonConvert.SerializeObject(this,settings);
			var tokenJson = JsonConvert.SerializeObject(CurrentToken, settings);
			CurrentToken = null;
			
			var  msg = new Message
			{
				Data = tokenJson,
				MonitorId = Id,
				MsgType = MessageType.TokenRespond,
				SendingServer = RemoteServerGroup.Instance.NodeAddress
			};
			var remoteDispatch = new RemoteDispatch(msg,destination);
			RemoteServerGroup.Instance.SendQueue.Add(remoteDispatch);
		}
	}
}