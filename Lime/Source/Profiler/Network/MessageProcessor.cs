#if PROFILER

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Lime.Profiler.Formatting;
using Yuzu.Binary;

namespace Lime.Profiler.Network
{
	using Task = System.Threading.Tasks.Task;

	/// <summary>
	/// Performs message queue processing on the client and server.
	/// </summary>
	internal class MessageProcessor : IConnection
	{
		/// <summary>
		/// Time between attempts to receive and send messages.
		/// </summary>
		private const int IterationDelay = 4 * 1000 / 60;

		private ConcurrentQueue<object> awaitingSend;

		private IInterrupter interrupter;

		private TcpClient client;

		private NetworkStream networkStream;
		private BinaryReader inputStream;
		private BinaryWriter outputStream;

		private BinarySerializer serializer;
		private BinaryDeserializer deserializer;

		private CancellationTokenSource cancellationTokenSource;
		private bool isRemoteCancellationRequest;

		/// <inheritdoc/>
		public bool IsAlive { get; private set; }

		/// <inheritdoc/>
		public event Action Closed;

		/// <inheritdoc/>
		public void LazySend(object @object) => awaitingSend.Enqueue(@object);

		/// <inheritdoc/>
		public void RequestClose()
		{
			Application.Exited -= RequestClose;
			cancellationTokenSource?.Cancel();
		}

		/// <summary>
		/// Starts message processing on top of a successfully configured connection.
		/// </summary>
		/// <remarks>
		/// Will automatically close the client.
		/// </remarks>
		public void RunOverConnection(TcpClient client, IInterrupter interrupter)
		{
			if (this.client != null) {
				throw new InvalidOperationException("Reinitialization detected!");
			}
			IsAlive = true;
			this.client = client;
			this.interrupter = interrupter;
			serializer = new BinarySerializer();
			deserializer = new BinaryDeserializer();
			networkStream = client.GetStream();
			inputStream = new BinaryReader(new BufferedStream(networkStream));
			outputStream = new BinaryWriter(new BufferedStream(networkStream));
			cancellationTokenSource = new CancellationTokenSource();
			async void Run() {
				try {
					Application.Exited += RequestClose;
					var cancellationToken = cancellationTokenSource.Token;
					while (!cancellationToken.IsCancellationRequested) {
						while (networkStream.DataAvailable) {
							var message = deserializer.FromReader(inputStream);
							isRemoteCancellationRequest =
								(message as ServiceMessage)?.IsCloseConnectionRequested ?? false;
							if (isRemoteCancellationRequest) {
								break;
							}
							this.interrupter.AfterDeserialization(inputStream, message);
						}
						if (isRemoteCancellationRequest) {
							break;
						}
						bool isSomethingSent = false;
						while (!awaitingSend.IsEmpty) {
							awaitingSend.TryDequeue(out object message);
							if (message != null) {
								serializer.ToWriter(message, outputStream);
								this.interrupter.AfterSerialization(outputStream, message);
								isSomethingSent = true;
							}
						}
						if (!isSomethingSent) {
							serializer.ToWriter(new ServiceMessage(), outputStream);
						}
						outputStream.Flush();
						await Task.Delay(IterationDelay, cancellationToken);
					}
				} catch (TaskCanceledException) {
					// Suppress
				} catch (System.Exception exception) {
					// Suppress
					System.Console.WriteLine(exception);
					Connection.Exceptions.Push(exception);
				} finally {
					cancellationTokenSource = null;
					try {
						if (!isRemoteCancellationRequest) {
							var message = new ServiceMessage {
								IsCloseConnectionRequested = true
							};
							serializer.ToWriter(message, outputStream);
							outputStream.Flush();
						}
					} catch (System.Exception exception) {
						// Suppress
						System.Console.WriteLine(exception);
						Connection.Exceptions.Push(exception);
					} finally {
						inputStream?.Dispose();
						outputStream?.Dispose();
						client?.Close();
						IsAlive = false;
						Application.InvokeOnNextUpdate(() => Closed?.Invoke());
					}
				}
			}
			Run();
		}
	}
}

#endif // PROFILER
