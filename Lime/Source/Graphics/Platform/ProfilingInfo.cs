using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Yuzu;

namespace Lime.Graphics.Platform
{
	public static class SceneProfilingInfo
	{
		/// <summary>
		/// Node manager for scene objects.
		/// </summary>
		public static object NodeManager;
	}

	/// <summary>
	/// Used to separate draw calls some objects
	/// from others. Shared by multiple draw calls.
	/// </summary>
	public class ProfilingInfo
	{
		private static Stack<ProfilingInfo> freeInstances = new Stack<ProfilingInfo>();

		[YuzuExclude]
		private bool isFree;

		/// <summary>
		/// The objects that created this draw call.
		/// <list type="bullet">
		/// <item><description>
		/// If <see cref="ProfilingInfo"/> created on the local device,
		/// this can be Node or List of Node or null.
		/// </description></item>
		/// <item><description>
		/// If <see cref="ProfilingInfo"/> received from outside,
		/// this can be Node.Id or List of Node.Id or null.
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
		/// This is a link to the material, if <see cref="ProfilingInfo"/> created on the local device.
		/// </description></item>
		/// <item><description>
		/// This is a string with the name of the material, if <see cref="ProfilingInfo"/> received from outside.
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ProfilingInfo Acquire(object material = null, int passIndex = 0)
		{
			bool isPartOfScene =
				SceneProfilingInfo.NodeManager == null ||
				RenderObjectOwnersInfo.CurrentManager == null ||
				ReferenceEquals(RenderObjectOwnersInfo.CurrentManager, SceneProfilingInfo.NodeManager);
			return Acquire(RenderObjectOwnersInfo.CurrentNode, isPartOfScene, material, passIndex);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
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
