using System.Collections.Generic;

namespace Lime.Graphics.Platform
{
	public class RenderObjectOwnersInfo
	{
		private static Stack<object> nodes;
		private static Stack<object> managers;

		public static object CurrentNode { get; protected set; }
		public static object CurrentManager { get; protected set; }

		static RenderObjectOwnersInfo()
		{
			nodes = new Stack<object>();
			managers = new Stack<object>();
			nodes.Push(null);
			managers.Push(null);
		}

		private object node;
		private object manager;

		public object Node => node;

		/// <summary>
		/// Must be called AFTER each <see cref="IPresenter.GetRenderObject(Node)"/>.
		/// </summary>
		public void SetOwnersInfo(object node, object manager)
		{
			this.node = node;
			this.manager = manager;
		}

		/// <summary>
		/// Must be called BEFORE each <see cref="Render"/>.
		/// </summary>
		public void SetGlobalProfilerData()
		{
			CurrentNode = node;
			CurrentManager = manager;
			nodes.Push(node);
			managers.Push(manager);
		}

		/// <summary>
		/// Must be called AFTER each <see cref="Render"/>.
		/// </summary>
		public void ResetGlobalProfilerData()
		{
			nodes.Pop();
			managers.Pop();
			CurrentNode = nodes.Peek();
			CurrentManager = managers.Peek();
		}
	}
}
