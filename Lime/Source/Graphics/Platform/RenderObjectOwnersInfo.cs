namespace Lime.Graphics.Platform
{
	public class RenderObjectOwnersInfo
	{
		public static object CurrentNode { get; protected set; }
		public static object CurrentManager { get; protected set; }

		private object node;
		private object manager;

		/// <summary>
		/// Must be called AFTER each <see cref="IPresenter.GetRenderObject(Node)"/>.
		/// </summary>
		public void SetProfilerData(object node, object manager)
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
		}

		/// <summary>
		/// Must be called AFTER each <see cref="Render"/>.
		/// </summary>
		public void ResetGlobalProfilerData()
		{
			CurrentNode = null;
			CurrentManager = null;
		}
	}
}
