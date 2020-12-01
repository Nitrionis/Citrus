#if PROFILER

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

		private readonly List<System.Exception> suppressedExceptions;

		/// <inheritdoc/>
		public ReadOnlyCollection<System.Exception> SuppressedExceptions { get; }

		/// <inheritdoc/>
		public event Action Closed
		{
			add => messageProcessor.Closed += value;
			remove => messageProcessor.Closed -= value;
		}

		public Client(IPEndPoint endPoint, IInterrupter interrupter)
		{
			suppressedExceptions = new List<System.Exception>();
			SuppressedExceptions = new ReadOnlyCollection<System.Exception>(suppressedExceptions);
			TcpClient client = null;
			try {
				client = new TcpClient();
				client.Connect(endPoint);
				Connection.Decorate(client);
				messageProcessor = new MessageProcessor(exceptionsStorage: suppressedExceptions);
				messageProcessor.RunOverConnection(client, interrupter);
			} catch (System.Exception exception) {
				// Suppress
				System.Console.WriteLine(exception);
				suppressedExceptions.Add(exception);
				client?.Close();
			}
		}

		/// <inheritdoc/>
		public void LazySend(IMessage message) => messageProcessor?.LazySend(message);

		/// <inheritdoc/>
		public void RequestClose() => messageProcessor?.RequestClose();
	}
}

#endif // PROFILER
