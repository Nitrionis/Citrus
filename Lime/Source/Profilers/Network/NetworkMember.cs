using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Yuzu;
using Yuzu.Binary;

namespace Lime.Profilers.Network
{
	internal class ServiceMessage : IItem
	{
		[YuzuRequired]
		public bool IsEmpty { get; set; }
		[YuzuRequired]
		public bool IsCloseRequested { get; set; }
	}

	internal abstract class NetworkMember : IConnection
	{
		/// <summary>
		/// Connection timeout in milliseconds.
		/// </summary>
		protected const int Timeout = 2000;

		public bool IsConnected { get; protected set; }

		public ConcurrentQueue<IItem> Received { get; }
		public Action OnReceived { get; set; }

		protected TcpClient client;
		protected NetworkStream stream;
		protected readonly LingerOption lingerOption;

		protected Thread thread;
		protected AutoResetEvent continueEvent;

		protected BinarySerializer serializer;
		protected BinaryDeserializer deserializer;

		protected bool isCloseRequested = false;
		protected bool isRemoteCloseRequest = false;
		public Action Closed { get; set; }

		protected NetworkMember()
		{
			Received = new ConcurrentQueue<IItem>();
			continueEvent = new AutoResetEvent(initialState: false);
			serializer = new BinarySerializer();
			deserializer = new BinaryDeserializer();
			lingerOption = new LingerOption(true, 2);
		}

		public void RequestClose()
		{
			IsConnected = false;
			isCloseRequested = true;
			isRemoteCloseRequest = false;
		}

		public void SerializeAndSend(IItem item) => serializer.ToStream(item, stream);

		public abstract bool TryLaunch(IPEndPoint ipEndPoint);

		protected void CloseConnection()
		{
			if (client != null) {
				client.Close();
			}
		}

		protected void CheckReceived()
		{
			while (stream.DataAvailable) {
				var item = (IItem)deserializer.FromStream(stream);
				if (!item.IsEmpty) {
					if (item.IsCloseRequested) {
						isCloseRequested = true;
						isRemoteCloseRequest = true;
						break;
					} else {
						Received.Enqueue(item);
						OnReceived?.Invoke();
					}
				}
			}
		}
	}
}
