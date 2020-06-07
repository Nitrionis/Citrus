using System.Collections.Generic;

namespace Lime.Graphics.Platform.Profiling
{
	public class RenderObjectOwnersInfo
	{
		private static Stack<IReferencesTableCompatible> nodes;
		private static Stack<object> managers;

		public static IReferencesTableCompatible CurrentNode { get; protected set; }
		public static object CurrentManager { get; protected set; }

		static RenderObjectOwnersInfo()
		{
			nodes = new Stack<IReferencesTableCompatible>();
			managers = new Stack<object>();
			nodes.Push(null);
			managers.Push(null);
		}

		public IReferencesTableCompatible Node { get; private set; }
		public object Manager { get; private set; }

		/// <summary>
		/// Must be called AFTER each <see cref="IPresenter.GetRenderObject(Node)"/>.
		/// </summary>
		public void SetOwnersInfo(IReferencesTableCompatible node, object manager)
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
			nodes.Push(Node);
			managers.Push(Manager);
		}

		/// <summary>
		/// Must be called AFTER each <see cref="RenderObject.Render"/>.
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
