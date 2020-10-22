#if PROFILER

using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using Yuzu;

namespace Lime.Profiler.Network
{
	internal class ServiceMessage
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
		event Action Closed;

		/// <summary>
		/// It puts the data in a queue to sending.
		/// </summary>
		/// <remarks>
		/// To serialize the object will be used Yuzu.
		/// </remarks>
		void LazySend(object @object);
	}

	internal static class Connection
	{
		/// <summary>
		/// Connection timeout in milliseconds.
		/// </summary>
		public const int Timeout = 5000;

		public static readonly ConcurrentStack<System.Exception> Exceptions;

		static Connection() => Exceptions = new ConcurrentStack<System.Exception>();

		public static void Decorate(TcpClient client)
		{
			client.LingerState = new LingerOption(true, 1);
			var stream = client.GetStream();
			stream.ReadTimeout = Timeout;
		}
	}
}

#endif // PROFILER
