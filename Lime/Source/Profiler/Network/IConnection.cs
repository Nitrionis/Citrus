#if PROFILER

using System;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using Yuzu;

namespace Lime.Profiler.Network
{
	internal class Message
	{
		private bool isSerialized;

		public bool IsSerialized
		{
			get => isSerialized;
			set => isSerialized |= value;
		}

		public readonly object SendLockObject = new object();
	}

	internal sealed class ServiceMessage
	{
		/// <summary>
		/// Indicates that disconnect requested.
		/// </summary>
		[YuzuRequired]
		public bool IsCloseConnectionRequested { get; set; }
	}

	internal interface IConnection
	{
		/// <summary>
		/// Indicates whether a remote network member is connected.
		/// </summary>
		bool IsAlive { get; }

		/// <summary>
		/// Requests to close the connection.
		/// </summary>
		void RequestClose();

		/// <summary>
		/// Invoked after the connection closed.
		/// </summary>
		/// <remarks>
		/// The call will occur in the update thread.
		/// </remarks>
		event Action Closed;

		/// <summary>
		/// It puts the data in a queue to sending.
		/// </summary>
		/// <remarks>
		/// To serialize the object will be used Yuzu.
		/// </remarks>
		void LazySend(Message message);

		/// <summary>
		/// If the first message in queue is sending at the time of the call,
		/// we wait for the completion of its sending, otherwise we block the
		/// asynchronous sending of messages and synchronously send the message.
		/// </summary>
		void SendSynchronouslyFirstInQueue();

		/// <summary>
		/// List of errors that were suppressed while the connection was running.
		/// </summary>
		/// <remarks>
		/// The collection is never cleared.
		/// </remarks>
		ReadOnlyCollection<System.Exception> SuppressedExceptions { get; }
	}

	internal static class Connection
	{
		/// <summary>
		/// Connection timeout in milliseconds.
		/// </summary>
		public const int Timeout = 5000;

		public static void Decorate(TcpClient client)
		{
			client.LingerState = new LingerOption(true, 1);
			var stream = client.GetStream();
			stream.ReadTimeout = Timeout;
		}
	}
}

#endif // PROFILER
