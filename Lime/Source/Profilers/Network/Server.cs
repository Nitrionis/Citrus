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

		private TcpListener listener;
		private Stopwatch stopwatch;
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
				listener = new TcpListener(ipEndPoint);
				listener.Start();
				thread = new Thread(() => {
					Thread.CurrentThread.IsBackground = true;
					try {
						WaitConnectionRequest();
						InitializeConnection();
						while (!isCloseRequested) {
							CheckReceived();
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
			}
			return isSuccessfullyCreated;
		}

		private void WaitConnectionRequest()
		{
			while (!listener.Pending() && !isCloseRequested) {
				Thread.Sleep(100);
			}
		}

		private void InitializeConnection()
		{
			if (!isCloseRequested) {
				client = listener.AcceptTcpClient();
				listener.Stop();
				client.LingerState = lingerOption;
				stream = client.GetStream();
				stream.ReadTimeout = NetworkMember.Timeout;
				IsConnected = true;
			}
		}

		private void Sleep()
		{
			if (timeSinceLastReceive > NetworkMember.Timeout) {
				isCloseRequested = true;
				isRemoteMemberClosed = true;
			}
			stopwatch.Restart();
			bool waitRes = continueEvent.WaitOne(NetworkMember.Timeout / 2);
			stopwatch.Stop();
			if (waitRes) {
				if (stream.DataAvailable) {
					timeSinceLastReceive = 0;
				} else {
					timeSinceLastReceive += (int)stopwatch.ElapsedMilliseconds;
				}
			} else {
				timeSinceLastReceive += NetworkMember.Timeout / 2;
			}
		}
	}
}
