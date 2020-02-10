using System;
using System.Net;
using System.Collections.Generic;

namespace Lime.Profilers.Network
{
	public interface Item
	{
		/// <summary>
		/// Empty messages are used to maintain a connection.
		/// </summary>
		bool IsEmpty { get; }
	}

	internal interface IConnection
	{
		/// <summary>
		/// Indicates if a remote network member is connected.
		/// </summary>
		bool IsConnected { get; }

		/// <summary>
		/// Trying to launch client and connect to server or trying to launch server.
		/// </summary>
		bool TryLaunch(IPEndPoint ipEndPoint);

		/// <summary>
		/// Requests to close the connection.
		/// </summary>
		/// <param name="closedAction">Optional parameter. Invoked after the connection is closed.</param>
		void RequestClose(Action closedAction);

		/// <summary>
		/// Queue of received objects.
		/// </summary>
		Queue<Item> Received { get; }

		/// <summary>
		/// Invoked when a new object is received.
		/// </summary>
		Action OnReceived { get; set; }

		/// <summary>
		/// Serializes and sends data.
		/// </summary>
		void SerializeAndSend(Item item);
	}
}
