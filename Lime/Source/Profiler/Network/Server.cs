#if PROFILER

using System;
using System.Net;
using System.Net.Sockets;
using Lime.Profiler.Formatting;

namespace Lime.Profiler.Network
{
	using Task = System.Threading.Tasks.Task;

	internal class Server : IConnection
	{
		private MessageProcessor messageProcessor;
		private volatile bool isCloseRequested;

		/// <inheritdoc/>
		public bool IsAlive => messageProcessor.IsAlive;

		/// <inheritdoc/>
		public event Action Closed
		{
			add => messageProcessor.Closed += value;
			remove => messageProcessor.Closed -= value;
		}

		public Server(IPEndPoint endPoint, IInterrupter interrupter)
		{
			messageProcessor = new MessageProcessor();
			async void Run() {
				TcpClient client = null;
				try {
					var listener = new TcpListener(endPoint);
					listener.Start();
					while (!isCloseRequested && !listener.Pending()) {
						await Task.Delay(millisecondsDelay: 250);
					}
					client = listener.AcceptTcpClient();
					listener.Stop();
					Connection.Decorate(client);
					messageProcessor.RunOverConnection(client, interrupter);
				} catch (System.Exception exception) {
					// Suppress
					System.Console.WriteLine(exception);
					Connection.Exceptions.Push(exception);
					client?.Close();
				}
			}
			Run();
		}

		/// <inheritdoc/>
		public void LazySend(object @object) => messageProcessor?.LazySend(@object);

		/// <inheritdoc/>
		public void RequestClose()
		{
			isCloseRequested = true;
			messageProcessor?.RequestClose();
		}
	}
}

#endif // PROFILER
