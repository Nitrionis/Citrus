using System;
using System.Collections.Generic;
using System.Net;

namespace Lime.Profilers.Network
{
	internal class Client : IConnection
	{
		public bool IsConnected => throw new NotImplementedException();

		public Client()
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
