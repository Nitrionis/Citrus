using System.Collections.Generic;

namespace Lime.Profiler.Graphics
{
	public struct RenderObjectOwnerInfo
	{
		private static readonly Stack<RenderObjectOwnerInfo> descriptions;

		public static IProfileableObject CurrentNode { get; private set; }

		static RenderObjectOwnerInfo()
		{
			descriptions = new Stack<RenderObjectOwnerInfo>();
			descriptions.Push(new RenderObjectOwnerInfo { Node = null });
		}

		public IProfileableObject Node { get; private set; }

		/// <summary>
		/// Must be called AFTER each <see cref="IPresenter.GetRenderObject(Node)"/>.
		/// </summary>
		public void Initialize(IProfileableObject node) => Node = node;

		/// <summary>
		/// Must be called BEFORE each <see cref="RenderObject.Render"/>.
		/// </summary>
		public static void PushState(RenderObjectOwnerInfo ownerInfo)
		{
			CurrentNode = ownerInfo.Node;
			descriptions.Push(ownerInfo);
			RenderThreadProfilingInfo.PushState(CurrentNode?.IsPartOfScene ?? false);
		}

		/// <summary>
		/// Must be called AFTER each <see cref="RenderObject.Render"/>.
		/// </summary>
		public static void PopState()
		{
			descriptions.Pop();
			CurrentNode = descriptions.Peek().Node;
			RenderThreadProfilingInfo.PopState();
		}
	}
}
