#if PROFILER

using System;
using System.Net;
using System.Net.Sockets;
using Lime.Profiler.Formatting;

namespace Lime.Profiler.Network
{
	internal class Client : IConnection
	{
		private readonly MessageProcessor messageProcessor;

		/// <inheritdoc/>
		public bool IsAlive => messageProcessor.IsAlive;

		/// <inheritdoc/>
		public event Action Closed
		{
			add => messageProcessor.Closed += value;
			remove => messageProcessor.Closed -= value;
		}

		public Client(IPEndPoint endPoint, IInterrupter interrupter)
		{
			TcpClient client = null;
			try {
				client = new TcpClient();
				client.Connect(endPoint);
				Connection.Decorate(client);
				messageProcessor = new MessageProcessor();
				messageProcessor.RunOverConnection(client, interrupter);
			} catch (System.Exception exception) {
				// Suppress
				System.Console.WriteLine(exception);
				Connection.Exceptions.Push(exception);
				client?.Close();
			}
		}

		/// <inheritdoc/>
		public void LazySend(object @object) => messageProcessor?.LazySend(@object);

		/// <inheritdoc/>
		public void RequestClose() => messageProcessor?.RequestClose();
	}
}

#endif // PROFILER
