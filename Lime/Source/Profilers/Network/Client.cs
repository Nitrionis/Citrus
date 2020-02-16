using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;

namespace Lime.Profilers.Network
{
	internal class Client : NetworkMember
	{
		private static readonly string MsgPrefix = "Profiler Client ";

		private ConcurrentQueue<IItem> awaitingSend;

		public Client()
		{
			awaitingSend = new ConcurrentQueue<IItem>();
		}

		/// <summary>
		/// It puts the data in a queue to sending.
		/// </summary>
		public void LazySerializeAndSend(IItem item)
		{
			awaitingSend.Enqueue(item);
			if (!continueEvent.Set()) {
				throw new InvalidOperationException();
			}
		}

		public override bool TryLaunch(IPEndPoint ipEndPoint)
		{
			bool isSuccessfullyCreated = true;
			try {
				client = new TcpClient();
				client.Connect(ipEndPoint);
				client.LingerState = lingerOption;
				stream = client.GetStream();
				stream.ReadTimeout = NetworkMember.Timeout;
				IsConnected = true;
				thread = new Thread(() => {
					Thread.CurrentThread.IsBackground = true;
					try {
						while (!isCloseRequested) {
							CheckReceived();
							SendAwaiting();
							Sleep();
						}
						if (!isRemoteMemberClosed) {
							SerializeAndSend(new ServiceMessage { IsCloseRequested = true });
						}
					} catch (SocketException e) {
						Debug.Write("{0}: {1}", MsgPrefix, e);
					} catch (IOException e) {
						Debug.Write("{0}: {1}", MsgPrefix, e);
					} finally {
						CloseConnection();
						IsConnected = false;
						OnClosed?.Invoke();
					}
				});
				thread.Start();
			} catch (SocketException e) {
				Debug.Write("{0}: {1}", MsgPrefix, e);
				isSuccessfullyCreated = false;
			} catch (IOException e) {
				Debug.Write("{0}: {1}", MsgPrefix, e);
				isSuccessfullyCreated = false;
			}
			return isSuccessfullyCreated;
		}

		private void SendAwaiting()
		{
			if (!isCloseRequested) {
				bool isSent = false;
				while (awaitingSend.Count > 0) {
					IItem item;
					if (awaitingSend.TryDequeue(out item)) {
						serializer.ToStream(item, stream);
					} else {
						throw new InvalidOperationException();
					}
					isSent = true;
				}
				if (!isSent) {
					SerializeAndSend(new ServiceMessage { IsEmpty = true });
				}
			}
		}

		private void Sleep() => continueEvent.WaitOne(NetworkMember.Timeout / 2);
	}
}
