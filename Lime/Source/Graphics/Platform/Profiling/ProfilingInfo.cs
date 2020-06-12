
namespace Lime.Graphics.Platform.Profiling
{
	public static class SceneProfilingInfo
	{
		/// <summary>
		/// Node manager for scene objects.
		/// </summary>
		public static object NodeManager;
	}

	/// <summary>
	/// Additional draw call parameters for profiling.
	/// </summary>
	public class ProfilingInfo
	{
		private static ProfilingInfo instance = new ProfilingInfo();

		/// <summary>
		/// The indices of objects in a ReferencesTable that created this draw call.
		/// </summary>
		public Owners Owners;

		/// <summary>
		/// True if at least one owner belongs to the scene.
		/// </summary>
		public bool IsPartOfScene;

		/// <summary>
		/// Material type index in <see cref="MaterialsTable"/> used during rendering.
		/// </summary>
		public uint MaterialIndex;

		/// <summary>
		/// Current render pass index of the material.
		/// </summary>
		public int RenderPassIndex;

		public ProfilingInfo() { }

		public static ProfilingInfo Acquire(object material, int passIndex = 0)
		{
			bool isPartOfScene =
#if ANDROID || iOS
				true;
#else
				SceneProfilingInfo.NodeManager == null ||
				RenderObjectOwnersInfo.CurrentManager == null ||
				ReferenceEquals(RenderObjectOwnersInfo.CurrentManager, SceneProfilingInfo.NodeManager);
#endif
			var owners = NativeOwnersPool.Acquire(RenderObjectOwnersInfo.CurrentNode, Owners.ThreadBit.Render);
			uint materialIndex = NativeMaterialsTable.GetIndex(material.GetType());
			return Acquire(owners, isPartOfScene, materialIndex, passIndex);
		}

		public static ProfilingInfo Acquire(Owners owners, bool isPartOfScene, uint materialIndex, int passIndex)
		{
			instance.Owners = owners;
			instance.IsPartOfScene = isPartOfScene;
			instance.MaterialIndex = materialIndex;
			instance.RenderPassIndex = passIndex;
			return instance;
		}
	}
}
