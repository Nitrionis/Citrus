using System;
using System.Net;
using System.Collections.Concurrent;

namespace Lime.Profilers.Network
{
	/// <summary>
	/// Will be serialized using Yuzu.
	/// </summary>
	public interface IItem
	{
		/// <summary>
		/// Empty messages are used to maintain a connection.
		/// </summary>
		bool IsEmpty { get; }

		/// <summary>
		/// Indicates that disconnect requested.
		/// </summary>
		bool IsCloseRequested { get; set; }
	}

	internal interface IConnection
	{
		/// <summary>
		/// Indicates whether a remote network member is connected.
		/// </summary>
		bool IsConnected { get; }

		/// <summary>
		/// Trying to launch client and connect to server or trying to launch server.
		/// </summary>
		bool TryLaunch(IPEndPoint ipEndPoint);

		/// <summary>
		/// Requests to close the connection.
		/// </summary>
		void RequestClose();

		/// <summary>
		/// Invoked after the connection is closed.
		/// </summary>
		Action Closed { get; set; }

		/// <summary>
		/// Queue of received objects.
		/// </summary>
		ConcurrentQueue<IItem> Received { get; }

		/// <summary>
		/// Invoked when a new object is received.
		/// </summary>
		Action OnReceived { get; set; }

		/// <summary>
		/// Serializes and sends data to remote member.
		/// </summary>
		void SerializeAndSend(IItem item);
	}
}
