
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using CommandLine;
using CSharpx;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;

namespace Monitor
{
	public class RemoteInfo
	{
		private const string SubMessage = "sub";
		private const string JsonMainNodeAddress = "Main";
		private const string JsonRemoteAddressesList = "Remote";
		
		
		[JsonProperty(JsonMainNodeAddress)]
		public int MainNodeAddress { get; set; }
		
		[JsonProperty(JsonRemoteAddressesList)]
		public List<string> RemoteAddressesList { get; set; }

		private OptionsInit Init;
	
		public static RemoteInfo Initiate(string[] args)
		{
			var result = Parser.Default.ParseArguments<OptionsInit>(args).WithNotParsed(errors =>
			{
				foreach (var err in errors)
				{
					Console.WriteLine(err.ToString());
				}
				throw new Exception("Argument exception");
			});
			OptionsInit init = ((Parsed<OptionsInit>) result).Value;
			var remoteInfo = GetRemoteInfoFromFile(init.FileName);
			remoteInfo.Init = init;
			if (remoteInfo.Init.MainNode) remoteInfo.MainNode();
			else remoteInfo.SubNode();
			return remoteInfo;
		}

		private void MainNode()
		{
			RemoteAddressesList.RemoveAt(this.MainNodeAddress);
			using (var pushMainNode = new PushSocket(MainNodeAddress))
			{
				foreach (var address in RemoteAddressesList)
				{
					using (var pullNode = new PullSocket(address))
					{
						var information = pullNode.ReceiveFrameString();
						if (String.Equals(information, SubMessage))
						{
							
						}
						else throw new Exception("Recieved wrong message from node");
					}				
				}

			}
		}

		private void SubNode()
		{
			RemoteAddressesList.RemoveAt(this.Init.Number.GetValueOrDefault());	
			
		}

		private static RemoteInfo GetRemoteInfoFromFile(string fileName)
		{
			if (string.IsNullOrEmpty(fileName)) 
				throw new Exception("File name is null or empty");
		
			try
				{
					var jsonString = File.ReadAllText(fileName, Encoding.UTF8);
					var remoteInfo = JsonConvert.DeserializeObject<RemoteInfo>(jsonString);
					var count = remoteInfo.RemoteAddressesList.Count;
					if (remoteInfo.RemoteAddressesList == null ||
						remoteInfo.MainNodeAddress >= count ||
						remoteInfo.RemoteAddressesList.Count == 0)
						throw new Exception("Incorrect configuration file");
					return remoteInfo;
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
					throw;
				}
		}

	}
}