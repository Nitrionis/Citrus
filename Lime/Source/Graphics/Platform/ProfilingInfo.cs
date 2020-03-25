using System.Collections.Generic;
using Yuzu;

namespace Lime.Graphics.Platform
{
	/// <summary>
	/// Used to separate draw calls some objects
	/// from others. Shared by multiple draw calls.
	/// </summary>
	public class ProfilingInfo
	{
		private static Stack<ProfilingInfo> freeInstances = new Stack<ProfilingInfo>();

		/// <summary>
		/// Node manager for scene objects.
		/// </summary>
		public static object SceneNodeManager;

		[YuzuExclude]
		private bool isFree;

		/// <summary>
		/// The objects that created this draw call.
		/// <list type="bullet">
		/// <item><description>
		/// If <see cref="ProfilingInfo"/> created on the local device.
		/// This can be Node or List of Node or null.
		/// </description></item>
		/// <item><description>
		/// If <see cref="ProfilingInfo"/> received from outside.
		/// This can be Node.Id or List of Node.Id or null.
		/// </description></item>
		/// </list>
		/// </summary>
		[YuzuRequired]
		public object Owners;

		/// <summary>
		/// True if at least one owner belongs to the scene.
		/// </summary>
		[YuzuRequired]
		public bool IsPartOfScene;

		/// <summary>
		/// Material used during rendering.
		/// <list type="bullet">
		/// <item><description>
		/// If <see cref="ProfilingInfo"/> created on the local device.
		/// This is a link to the material.
		/// </description></item>
		/// <item><description>
		/// If <see cref="ProfilingInfo"/> received from outside.
		/// This is a string with the name of the material.
		/// </description></item>
		/// </list>
		/// </summary>
		[YuzuRequired]
		public object Material;

		/// <summary>
		/// Material render pass index.
		/// After rendering, this value makes no sense.
		/// </summary>
		[YuzuExclude]
		internal int CurrentRenderPassIndex;

		public ProfilingInfo() { }

		public static ProfilingInfo Acquire(object material = null, int passIndex = 0)
		{
			bool isPartOfScene =
				RenderObjectOwnersInfo.CurrentManager == null ||
				ReferenceEquals(RenderObjectOwnersInfo.CurrentManager, SceneNodeManager);
			return Acquire(RenderObjectOwnersInfo.CurrentNode, isPartOfScene, material, passIndex);
		}

		public static ProfilingInfo Acquire(object owners, bool isPartOfScene, object material, int passIndex)
		{
			var profilingInfo = freeInstances.Count > 0 ? freeInstances.Pop() : new ProfilingInfo();
			profilingInfo.isFree = false;
			profilingInfo.Owners = owners;
			profilingInfo.IsPartOfScene = isPartOfScene || owners == null;
			profilingInfo.Material = material;
			profilingInfo.CurrentRenderPassIndex = passIndex;
			return profilingInfo;
		}

		public void Free()
		{
			if (!isFree) {
				Owners = null;
				Material = null;
				freeInstances.Push(this);
				isFree = true;
			}
		}

		public class ClearMaterial
		{
			public static readonly ClearMaterial Instance = new ClearMaterial();
		}
	}
}
