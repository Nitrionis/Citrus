using System.Collections.Generic;

namespace Lime.Graphics.Platform.Profiling
{
	public class RenderObjectOwnersInfo
	{
		public static readonly RenderObjectOwnersInfo BatchInfo = new RenderObjectOwnersInfo();

		private struct OwnerDescription
		{
			public object Node;
			public object Manager;
		}

		private static readonly Stack<OwnerDescription> descriptions;

		public static object CurrentNode { get; protected set; }
		public static object CurrentManager { get; protected set; }

		static RenderObjectOwnersInfo()
		{
			descriptions = new Stack<OwnerDescription>();
			descriptions.Push(new OwnerDescription { Node = null, Manager = null });
		}

		public object Node { get; private set; }
		public object Manager { get; private set; }

		/// <summary>
		/// Must be called AFTER each <see cref="IPresenter.GetRenderObject(Node)"/>.
		/// </summary>
		public void SetOwnersInfo(object node, object manager)
		{
			Node = node;
			Manager = manager;
		}

		/// <summary>
		/// Must be called BEFORE each <see cref="RenderObject.Render"/>.
		/// </summary>
		public void SetGlobalProfilerData()
		{
			CurrentNode = Node;
			CurrentManager = Manager;
			descriptions.Push(new OwnerDescription { Node = Node, Manager = Manager });
		}

		/// <summary>
		/// Must be called AFTER each <see cref="RenderObject.Render"/>.
		/// </summary>
		public void ResetGlobalProfilerData()
		{
			descriptions.Pop();
			var top = descriptions.Peek();
			CurrentNode = top.Node;
			CurrentManager = top.Manager;
		}
	}
}
