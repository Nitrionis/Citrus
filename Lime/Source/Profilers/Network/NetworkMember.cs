using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Yuzu;
using Yuzu.Binary;

namespace Lime.Profilers.Network
{
	internal class ServiceMessage : Item
	{
		[YuzuRequired]
		public bool IsEmpty { get; set; }
		[YuzuRequired]
		public bool IsCloseRequested { get; set; }
	}

	internal abstract class NetworkMember : IConnection
	{
		/// <summary>
		/// Min wait period (send|receive) in milliseconds.
		/// </summary>
		private const int MinWaitPeriod = 1;

		/// <summary>
		/// Max wait period (send|receive) in milliseconds.
		/// </summary>
		private const int MaxWaitPeriod = 1000 / 16;

		public bool IsConnected { get; protected set; }

		public Queue<Item> Received { get; }
		public Action OnReceived { get; set; }

		protected TcpClient client;
		protected NetworkStream stream;
		protected readonly LingerOption lingerOption;

		protected Thread thread;

		protected BinarySerializer serializer;
		protected BinaryDeserializer deserializer;

		protected bool isCloseRequested = false;
		protected bool isRemoteMemberClosed = false;
		protected Action OnClosed = null;

		protected int waitPeriod = 16;
		protected Queue<int> waitingTimes;

		protected NetworkMember()
		{
			Received = new Queue<Item>();
			serializer = new BinarySerializer();
			deserializer = new BinaryDeserializer();
			lingerOption = new LingerOption(true, 2);
			waitingTimes = new Queue<int>();
		}

		public void RequestClose(Action closedAction)
		{
			isCloseRequested = true;
			isRemoteMemberClosed = false;
		}

		public void SerializeAndSend(Item item) => serializer.ToStream(item, stream);

		public abstract bool TryLaunch(IPEndPoint ipEndPoint);

		protected void CloseConnection()
		{
			if (stream != null) {
				stream.Close();
			}
			if (client != null) {
				client.Close();
			}
		}

		/// <summary>
		/// Calculates the waiting period before the next attempt to receive or send data.
		/// </summary>
		/// <param name="isItemProcessed">True if data was received or sent.</param>
		/// <returns>Wait period.</returns>
		protected int CalculateNextWaitPeriod(bool isItemProcessed)
		{
			int value;
			if (isItemProcessed) {
				waitPeriod += waitingTimes.Count > 1 ? 1 : -1;
				waitPeriod = Math.Max(MinWaitPeriod, Math.Min(MaxWaitPeriod, waitPeriod));
				value = waitPeriod;
				waitingTimes.Clear();
			} else {
				value = Math.Max(1, waitingTimes.Peek() / 2);
			}
			waitingTimes.Enqueue(value);
			return value;
		}
	}
}
