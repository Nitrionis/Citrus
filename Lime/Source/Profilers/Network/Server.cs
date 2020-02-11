using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;
using Yuzu.Binary;

namespace Lime.Profilers.Network
{
	internal class Server : NetworkMember
	{
		private static readonly string MsgPrefix = "Profiler Server ";

		private TcpListener listener;

		private int receivePeriod = 16;
		private Queue<int> lastWaits;

		public Server()
		{
			lastWaits = new Queue<int>();
		}

		public override bool TryLaunch(IPEndPoint ipEndPoint)
		{
			bool isSuccessfullyCreated = true;
			try {
				listener = new TcpListener(ipEndPoint);
				listener.Start();
				thread = new Thread(() => {
					try {
						client = listener.AcceptTcpClient();
						client.LingerState = lingerOption;
						stream = client.GetStream();
						stream.ReadTimeout = 3;
						IsConnected = true;
						while (!isCloseRequested) {
							bool isItemReceived = false;
							if (stream.DataAvailable) {
								isItemReceived = true;
								var item = (Item)deserializer.FromStream(stream);
								if (!item.IsEmpty) {
									if (item.IsCloseRequested) {
										isCloseRequested = true;
										isRemoteMemberClosed = true;
									} else {
										Received.Enqueue(item);
										OnReceived?.Invoke();
									}
								}
							}
							Thread.Sleep(CalculateNextWaitPeriod(isItemReceived));
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
			}
			return isSuccessfullyCreated;
		}
	}
}
