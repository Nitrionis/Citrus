#if PROFILER

using System.Diagnostics;

namespace Lime.Profiler
{
	public static class CpuUsageReasonsExtension
	{
		public static bool HasFlag(this CpuUsage.Reasons reason, CpuUsage.Reasons flag) => (reason & flag) == flag;
	}

	/// <summary>
	/// Describes CPU usage interval.
	/// </summary>
	public struct CpuUsage
	{
		/// <summary>
		/// Reason for using a processor.
		/// </summary>
		public Reasons Reason;

		/// <summary>
		/// Object type identifier which or for which CPU time is spent.
		/// </summary>
		public TypeIdentifier TypeIdentifier;

		/// <summary>
		/// List of row indices in a ReferencesTable.
		/// </summary>
		public Owners Owners;

		/// <summary>
		/// The <see cref="Stopwatch"/> timestamp of the start of the usage interval.
		/// </summary>
		public long StartTime;

		/// <summary>
		/// The <see cref="Stopwatch"/> timestamp of the finish of the usage interval.
		/// </summary>
		public long FinishTime;

		public enum Reasons : uint
		{
			/// <summary>
			/// If this bit is set to 1, the interpretation of the remaining bits is left to the game team.
			/// </summary>
			TeamBit = 1u << 31,

			/// <summary>
			/// Describes the time span from the beginning of this
			/// frame update to the beginning of the next frame update.
			/// </summary>
			FullUpdate = 1,

			/// <summary>
			/// Describes the time span from the beginning of this
			/// frame render to the beginning of the next frame render.
			/// </summary>
			/// <remarks>
			/// Rendering refers to the process of writing commands for drawing
			/// objects to command buffers and sending these buffers to the GPU.
			/// </remarks>
			FullRender = 2,

			/// <summary>
			/// Describes cost of <see cref="Animation.Advance(float)"/>.
			/// </summary>
			NodeAnimation = 3,

			/// <summary>
			/// Describes cost of <see cref="NodeCompatibilityExtensions.Update(Node, float)"/>.
			/// </summary>
			NodeUpdate = 4,

			/// <summary>
			/// Describes cost of <see cref="Lime.NodeProcessor.Update(float)"/>.
			/// </summary>
			NodeProcessor = 5,

			/// <summary>
			/// Describes cost of <see cref="BehaviorComponent.Update(float)"/>.
			/// </summary>
			BehaviorComponentUpdate = 6,

			/// <summary>
			/// Describes CPU usage to get a <see cref="RenderObject"/> which performed in update thread.
			/// </summary>
			NodeRenderPreparation = 7,

			/// <summary>
			/// Rendering of a node can mean different processes in render thread.
			/// 1) Node rendering is a process of writing commands to command buffers
			///    and sending these buffers to the GPU for drawing the node.
			/// 2) Node rendering is the process of adding the node to a batch.
			///    In this case, several nodes will be drawn in one draw call.
			/// 3) Both of the above.
			/// </summary>
			NodeRender = 8,

			/// <summary>
			/// Batch rendering is a process in render thread of writing commands to command
			/// buffers and sending these buffers to the GPU for drawing nodes in the batch.
			/// </summary>
			BatchRender = 9,

			/// <summary>
			/// previous update      previous render
			/// ▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀ ▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀
			///                current update     wait         current render
			///                ▀▀▀▀▀▀▀▀▀▀▀▀▀▀ ▀▀▀▀▀▀▀▀▀▀▀ ▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀
			/// </summary>
			WaitForPreviousRendering = 10,

			/// <summary>
			/// It is used to describe the wait when acquiring the next buffer
			/// for rendering a frame on the GPU. If we observe latency at this
			/// point, then the GPU is not keeping up with the CPU.
			/// </summary>
			WaitForAcquiringSwapchainBuffer = 11,

			/// <summary>
			/// To be able to find the node that owns the "CpuUsage" structure,
			/// we maintain a kind of shadow hierarchy of nodes, where objects
			/// are represented as structures with basic information about the
			/// node. Sometimes we need to remove garbage in this hierarchy.
			/// </summary>
			ReferenceTableGarbageCollection = 12,

			/// <summary>
			/// The cost of executing code in Sync method body.
			/// </summary>
			SyncBodyExecution = 13,

			/// <summary>
			/// Performing scheduled actions for the update thread.
			/// </summary>
			RunScheduledActions = 14,

			/// <summary>
			/// Performing scheduled actions for the render thread.
			/// </summary>
			RunPendingActionsOnRendering = 15,

			/// <summary>
			/// Describes Node deserialization cost.
			/// </summary>
			NodeDeserialization = 16,

			/// <summary>
			/// Describes cost of <see cref="AudioSystem.Update"/>.
			/// </summary>
			AudioSystemUpdate = 17,

			/// <summary>
			/// Describes cost of CommonWindow RaiseUpdatingHelper CommandQueue.Instance.IssueCommands().
			/// </summary>
			IssueCommands = 18,

			/// <summary>
			/// Describes cost of CommonWindow RaiseUpdatingHelper CommandHandlerList.Global.ProcessCommands().
			/// </summary>
			ProcessCommands = 19,

			/// <summary>
			/// Resizing the profiler database.
			/// </summary>
			ProfilerDatabaseResizing = 20,

			/// <summary>
			/// Describes cost of <see cref="Node.LoadExternalScenes"/>.
			/// </summary>
			LoadExternalScenes = 21,

			/// <summary>
			/// Max reason index.
			/// </summary>
			MaxReasonIndex = 21,
			
			/// <summary>
			/// Mask all reasons without secondary data.
			/// </summary>
			ReasonsBitMask = 0xff,
			
			/// <summary>
			/// Batch breaking due to the fact that there is no link to the previous batch.
			/// </summary>
			/// <remarks>
			/// Can only exist together with <see cref="BatchRender"/>.
			/// </remarks>
			BatchBreakNullLastBatch = 1u << 30,

			/// <summary>
			/// Batch breaking due to different materials.
			/// </summary>
			/// <remarks>
			/// Can only exist together with <see cref="BatchRender"/>.
			/// </remarks>
			BatchBreakDifferentMaterials = 1u << 29,

			/// <summary>
			/// Batch breaking due to the number of render passes in the material.
			/// </summary>
			/// <remarks>
			/// Can only exist together with <see cref="BatchRender"/>.
			/// </remarks>
			BatchBreakMaterialPassCount = 1u << 28,

			/// <summary>
			/// Batch break due to vertex buffer overflow.
			/// </summary>
			/// <remarks>
			/// Can only exist together with <see cref="BatchRender"/>.
			/// </remarks>
			BatchBreakVertexBufferOverflow = 1u << 27,

			/// <summary>
			/// Batch break due to index buffer overflow.
			/// </summary>
			/// <remarks>
			/// Can only exist together with <see cref="BatchRender"/>.
			/// </remarks>
			BatchBreakIndexBufferOverflow = 1u << 26,

			/// <summary>
			/// Batch breaking due to using a different atlas.
			/// </summary>
			/// <remarks>
			/// Can only exist together with <see cref="BatchRender"/>.
			/// </remarks>
			BatchBreakDifferentAtlasOne = 1u << 25,

			/// <summary>
			/// Batch breaking due to using a different atlas.
			/// </summary>
			/// <remarks>
			/// Can only exist together with <see cref="BatchRender"/>.
			/// </remarks>
			BatchBreakDifferentAtlasTwo = 1u << 24,
			
			/// <summary>
			/// Index of the first bit of the batch break reasons.
			/// </summary>
			BatchBreakReasonsStartBit = 24,
			
			/// <summary>
			/// 
			/// </summary>
			BatchBreakReasonsBitMask =
				BatchBreakNullLastBatch |
				BatchBreakDifferentMaterials |
				BatchBreakMaterialPassCount |
				BatchBreakVertexBufferOverflow |
				BatchBreakIndexBufferOverflow |
				BatchBreakDifferentAtlasOne |
				BatchBreakDifferentAtlasTwo
		}
	}
}

#endif // PROFILER
