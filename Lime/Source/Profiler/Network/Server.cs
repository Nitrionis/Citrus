#if PROFILER

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using Lime.Profiler.Formatting;

namespace Lime.Profiler.Network
{
	using Task = System.Threading.Tasks.Task;

	internal class Server : IConnection
	{
		private readonly MessageProcessor messageProcessor;
		private volatile bool isRemoteDeviceHasBeenConnected;
		private volatile bool isCloseRequested;

		/// <summary>
		/// Whether a remote device has been connected.
		/// </summary>
		public bool IsRemoteDeviceHasBeenConnected => isRemoteDeviceHasBeenConnected;
		
		/// <inheritdoc/>
		public bool IsAlive => messageProcessor.IsAlive;

		private readonly List<System.Exception> suppressedExceptions;

		/// <inheritdoc/>
		public ReadOnlyCollection<System.Exception> SuppressedExceptions { get; }

		/// <inheritdoc/>
		public event Action Closed
		{
			add => messageProcessor.Closed += value;
			remove => messageProcessor.Closed -= value;
		}

		public Server(IPEndPoint endPoint, IInterrupter interrupter)
		{
			suppressedExceptions = new List<System.Exception>();
			SuppressedExceptions = new ReadOnlyCollection<System.Exception>(suppressedExceptions);
			messageProcessor = new MessageProcessor(exceptionsStorage: suppressedExceptions);
			async void Run()
			{
				TcpClient client = null;
				try {
					var listener = new TcpListener(endPoint);
					listener.Start();
					while (!isCloseRequested && !listener.Pending()) {
						await Task.Delay(millisecondsDelay: 250);
					}
					client = listener.AcceptTcpClient();
					listener.Stop();
					isRemoteDeviceHasBeenConnected = true;
					Connection.Decorate(client);
					messageProcessor.RunOverConnection(client, interrupter);
				} catch (System.Exception exception) {
					// Suppress
					System.Console.WriteLine(exception);
					suppressedExceptions.Add(exception);
					client?.Close();
				}
			}
			Run();
		}

		/// <inheritdoc/>
		public void LazySend(IMessage message)
		{
			if (messageProcessor.IsAlive) {
				messageProcessor.LazySend(message);
			}
		}

		/// <inheritdoc/>
		public void RequestClose()
		{
			isCloseRequested = true;
			if (messageProcessor.IsAlive) {
				messageProcessor.RequestClose();
			}
		}
	}
}

#endif // PROFILER
