using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Lime.Profilers.Network
{
	internal class Client : NetworkMember
	{
		private static readonly string MsgPrefix = "Profiler Client ";

		private Queue<Item> awaitingSend;

		public Client()
		{
			awaitingSend = new Queue<Item>();
		}

		public override bool TryLaunch(IPEndPoint ipEndPoint)
		{
			bool isSuccessfullyCreated = true;
			try {
				client = new TcpClient();
				client.Connect(ipEndPoint);
				client.LingerState = lingerOption;
				stream = client.GetStream();
				IsConnected = true;
				thread = new Thread(() => {
					try {
						while (!isCloseRequested) {
							bool isItemSent = awaitingSend.Count > 0;
							if (stream.DataAvailable) {
								var item = (Item)deserializer.FromStream(stream);
								isCloseRequested = item.IsCloseRequested;
								if (!isCloseRequested) {
									Received.Enqueue(item);
									OnReceived?.Invoke();
								}
							}
							if (!isCloseRequested && awaitingSend.Count > 0) {
								serializer.ToStream(awaitingSend.Dequeue(), stream);
							}
							Thread.Sleep(CalculateNextWaitPeriod(isItemSent));
						}
						if (!isRemoteMemberClosed) {
							SerializeAndSend(new ServiceMessage { IsCloseRequested = true });
						}
					} catch (SocketException e) {
						Debug.Write("{0} SocketException: {1}", MsgPrefix, e);
					} catch (IOException e) {
						Debug.Write("{0} IOException: {1}", MsgPrefix, e);
					} finally {
						CloseConnection();
						IsConnected = false;
						OnClosed?.Invoke();
					}
				});
				thread.Start();
			} catch (SocketException e) {
				Debug.Write("{0} SocketException: {1}", MsgPrefix, e);
				isSuccessfullyCreated = false;
			} catch (IOException e) {
				Debug.Write("{0} IOException: {1}", MsgPrefix, e);
				isSuccessfullyCreated = false;
			}
			return isSuccessfullyCreated;
		}

		/// <summary>
		/// It puts the data in a queue to sending.
		/// </summary>
		public void LazySerializeAndSend(Item item) => awaitingSend.Enqueue(item);
	}
}
