using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Lime.Profilers.Network
{
	internal class Server : NetworkMember
	{
		private static readonly string MsgPrefix = "Profiler Server ";

		public TcpListener Listener { get; private set; }
		private readonly Stopwatch stopwatch;
		private int timeSinceLastReceive;

		public Server()
		{
			stopwatch = new Stopwatch();
		}

		/// <summary>
		/// Awake the server. Initiates an attempt to receive messages.
		/// </summary>
		public void TryReceive()
		{
			if (!continueEvent.Set()) {
				throw new InvalidOperationException();
			}
		}

		public override bool TryLaunch(IPEndPoint ipEndPoint)
		{
			bool isSuccessfullyCreated = true;
			try {
				Listener = new TcpListener(ipEndPoint);
				Listener.Start();
				thread = new Thread(() => {
					Thread.CurrentThread.IsBackground = true;
					try {
						WaitConnectionRequest();
						InitializeConnection();
						if (client != null) {
							CheckReceived();
							do {
								Sleep();
								CheckReceived();
							} while (!isCloseRequested);
							if (!isRemoteCloseRequest) {
								SerializeAndSend(new ServiceMessage {
									IsCloseRequested = true
								});
							}
						}
					} catch (SocketException e) {
						Debug.Write("{0}: {1}", MsgPrefix, e);
					} catch (IOException e) {
						Debug.Write("{0}: {1}", MsgPrefix, e);
					} finally {
						Debug.Write("Server Closed!");
						CloseConnection();
						IsConnected = false;
						Closed?.Invoke();
					}
				});
				thread.Start();
			} catch (SocketException e) {
				Debug.Write("{0}: {1}", MsgPrefix, e);
				isSuccessfullyCreated = false;
			}
			return isSuccessfullyCreated;
		}

		private void WaitConnectionRequest()
		{
			while (!isCloseRequested && !Listener.Pending()) {
				Thread.Sleep(100);
			}
		}

		private void InitializeConnection()
		{
			if (!isCloseRequested) {
				client = Listener.AcceptTcpClient();
				Listener.Stop();
				client.LingerState = lingerOption;
				stream = client.GetStream();
				stream.ReadTimeout = NetworkMember.Timeout;
				IsConnected = true;
			}
		}

		private void Sleep()
		{
			stopwatch.Restart();
			bool waitRes = continueEvent.WaitOne(NetworkMember.Timeout / 2);
			stopwatch.Stop();
			if (waitRes || stream.DataAvailable) {
				if (stream.DataAvailable) {
					timeSinceLastReceive = 0;
				} else {
					timeSinceLastReceive += (int)stopwatch.ElapsedMilliseconds;
				}
			} else {
				timeSinceLastReceive += NetworkMember.Timeout / 2;
			}
			if (timeSinceLastReceive > NetworkMember.Timeout) {
				isCloseRequested = true;
				isRemoteCloseRequest = true;
			}
		}
	}
}
