using System;
using System.Net;

namespace Lime.Profilers.Network
{
	internal class Server : IConnection
	{
		public bool IsConnected => throw new NotImplementedException();

		public Server()
		{

		}

		/// <inheritdoc/>
		public bool TryLaunch(IPEndPoint ipEndPoint)
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public void RequestClose(Action closedAction)
		{
			throw new NotImplementedException();
		}
	}
}
