
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
	public class GpuCallInfo
	{
		private static GpuCallInfo instance = new GpuCallInfo();

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

		public GpuCallInfo() { }

		public static GpuCallInfo Acquire(object material, int passIndex = 0)
		{
			bool isPartOfScene =
#if ANDROID || iOS
				true;
#else
				SceneProfilingInfo.NodeManager == null ||
				RenderObjectOwnersInfo.CurrentManager == null ||
				ReferenceEquals(RenderObjectOwnersInfo.CurrentManager, SceneProfilingInfo.NodeManager);
#endif
			var owners = new Owners(ReferencesTable.InvalidReference);
			if (RenderObjectOwnersInfo.CurrentNode != null) {
				NativeNodesTables.CreateOrAddReferenceTo(RenderObjectOwnersInfo.CurrentNode);
				owners.AsIndex = RenderObjectOwnersInfo.CurrentNode.ReferenceTableRowIndex;
			}
			uint materialIndex = NativeMaterialsTable.GetIndex(material.GetType());
			return Acquire(owners, isPartOfScene, materialIndex, passIndex);
		}

		public static GpuCallInfo Acquire(Owners owners, bool isPartOfScene, uint materialIndex, int passIndex)
		{
			instance.Owners = owners;
			instance.IsPartOfScene = isPartOfScene;
			instance.MaterialIndex = materialIndex;
			instance.RenderPassIndex = passIndex;
			return instance;
		}
	}
}
