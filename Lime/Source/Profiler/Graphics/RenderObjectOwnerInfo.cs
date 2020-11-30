#if PROFILER
using System.Collections.Generic;

namespace Lime.Profiler.Graphics
{
	/// <summary>
	/// Used to pass profiling information to the depths of render system.
	/// </summary>
	public struct RenderObjectOwnerInfo
	{
		private static readonly Stack<RenderObjectOwnerInfo> descriptions;
		private static readonly IProfileableObject emptyProfileableObject;

		public static IProfileableObject CurrentNode { get; private set; }

		public static Owners CurrentOwner => new Owners(CurrentNode.RowIndex);

		static RenderObjectOwnerInfo()
		{
			descriptions = new Stack<RenderObjectOwnerInfo>();
			emptyProfileableObject = new EmptyProfileableObject();
			descriptions.Push(new RenderObjectOwnerInfo { Node = emptyProfileableObject });
		}

		public IProfileableObject Node { get; private set; }

		/// <summary>
		/// Must be called AFTER each <see cref="IPresenter.GetRenderObject(Node)"/>.
		/// </summary>
		public void Initialize(IProfileableObject node) => Node = node;

		/// <summary>
		/// Resets owner data.
		/// </summary>
		public void Reset() => Node = null;

		/// <summary>
		/// Must be called BEFORE each <see cref="RenderObject.Render"/>.
		/// </summary>
		public static void PushState(RenderObjectOwnerInfo ownerInfo)
		{
			CurrentNode = ownerInfo.Node ?? CurrentNode;
			descriptions.Push(new RenderObjectOwnerInfo { Node = CurrentNode });
		}

		/// <summary>
		/// Must be called AFTER each <see cref="RenderObject.Render"/>.
		/// </summary>
		public static void PopState()
		{
			descriptions.Pop();
			CurrentNode = descriptions.Peek().Node;
		}

		public static void GetRenderObjectCpuUsageFinished(
			ProfilerDatabase.CpuUsageStartInfo startInfo,
			IProfileableObject @object, ITypeIdentifierProvider renderObject)
		{
			@object = @object ?? emptyProfileableObject;
			ProfilerDatabase.EnsureDescriptionFor(@object);
			ProfilerDatabase.CpuUsageFinished(
				startInfo, new Owners(@object.RowIndex),
				CpuUsage.Reasons.NodeRenderPreparation,
				renderObject.Identifier);
		}

		private class EmptyProfileableObject : IProfileableObject
		{
			public string Name => null;
			public bool IsPartOfScene => false;
			public bool IsOverdrawForeground => false;
			public IProfileableObject Parent => null;
			public ReferenceTable.RowIndex RowIndex { get; set; } = ReferenceTable.RowIndex.Invalid;
			public TypeIdentifier Identifier { get; } = ProfilerDatabase.EnsureNumberFor(typeof(EmptyProfileableObject));
		}
	}
}
#endif // PROFILER
