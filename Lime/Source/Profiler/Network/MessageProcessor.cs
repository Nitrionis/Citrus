#if PROFILER

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

		private ConcurrentQueue<IMessage> awaitingSend;

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

		private readonly List<System.Exception> suppressedExceptions;

		/// <inheritdoc/>
		public ReadOnlyCollection<System.Exception> SuppressedExceptions => throw new NotSupportedException();

		public MessageProcessor(List<System.Exception> exceptionsStorage) => suppressedExceptions = exceptionsStorage;

		/// <inheritdoc/>
		public void LazySend(IMessage message) => awaitingSend.Enqueue(message);

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
			serializer = new BinarySerializer();
			deserializer = new BinaryDeserializer();
			networkStream = client.GetStream();
			inputStream = new BinaryReader(new BufferedStream(networkStream));
			outputStream = new BinaryWriter(new BufferedStream(networkStream));
			cancellationTokenSource = new CancellationTokenSource();
			async void Run()
			{
				try {
					Application.Exited += RequestClose;
					var cancellationToken = cancellationTokenSource.Token;
					while (!cancellationToken.IsCancellationRequested) {
						while (!interrupter.IsDeserializationPaused && networkStream.DataAvailable) {
							var message = deserializer.FromReader(inputStream);
							if (message is ServiceMessage sm && sm.IsCloseConnectionRequested) {
								isRemoteCancellationRequest = true;
								break;
							}
							interrupter.AfterDeserialization(inputStream, message);
						}
						if (isRemoteCancellationRequest) {
							break;
						}
						bool isSomethingSent = false;
						while (!interrupter.IsSerializationPaused && !awaitingSend.IsEmpty) {
							if (awaitingSend.TryDequeue(out IMessage message)) {
								serializer.ToWriter(message, outputStream);
								interrupter.AfterSerialization(outputStream, message);
								isSomethingSent = true;
							} else {
								throw new InvalidOperationException("Invalid processing order!");
							}
						}
						if (!isSomethingSent) {
							serializer.ToWriter(new ServiceMessage(), outputStream);
						}
						outputStream.Flush();
						await Task.Delay(IterationDelay);
					}
				} catch (TaskCanceledException) {
					// Suppress
				} catch (System.Exception exception) {
					// Suppress
					System.Console.WriteLine(exception);
					suppressedExceptions.Add(exception);
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
						suppressedExceptions.Add(exception);
					} finally {
						CloseClientAndStreams();
					}
				}
			}
			Run();
		}

		private void CloseClientAndStreams()
		{
			inputStream?.Dispose();
			outputStream?.Dispose();
			inputStream = null;
			outputStream = null;
			client?.Close();
			client = null;
			IsAlive = false;
			Application.InvokeOnNextUpdate(() => Closed?.Invoke());
		}
	}
}

#endif // PROFILER
