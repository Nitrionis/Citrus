#if PROFILER

using System;

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
		/// Interpretation depends on the <see cref="Reason"/> field.
		/// </summary>
		/// <remarks>
		/// 
		/// </remarks>
		public int TypeId;

		/// <summary>
		/// List of row indices in a ReferencesTable.
		/// </summary>
		public Owners Owners;

		/// <summary>
		/// The timestamp of the start of the usage interval in microseconds from the start of the frame update.
		/// </summary>
		public long StartTime;

		/// <summary>
		/// The timestamp of the finish of the usage interval in microseconds from the start of the frame update.
		/// </summary>
		public long FinishTime;

		/// <summary>
		/// The first 16 bits are reserved for use by the engine team,
		/// the remaining 16 bits can be used by the project team at its discretion.
		/// </summary>
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
			/// <remarks>The <see cref="TypeGuid"/> contains <see cref="Guid.Empty"/>.</remarks>
			FullUpdate = 1,

			/// <summary>
			/// Describes the time span from the beginning of this
			/// frame render to the beginning of the next frame render.
			/// </summary>
			/// <remarks>
			/// Rendering refers to the process of writing commands for drawing
			/// objects to command buffers and sending these buffers to the GPU.<para/>
			/// The <see cref="TypeGuid"/> contains <see cref="Guid.Empty"/>.
			/// </remarks>
			FullRender = 2,

			/// <summary>
			/// Describes cost of <see cref="Animation.Advance(float)"/>.
			/// </summary>
			/// <remarks>
			/// The <see cref="TypeGuid"/> contains <see cref="Animation"/> type GUID.
			/// </remarks>
			NodeAnimation = 3,

			/// <summary>
			/// Describes cost of <see cref="BehaviorComponent.Update(float)"/>.
			/// </summary>
			/// <remarks>
			/// The <see cref="TypeGuid"/> contains GUID of type derived from <see cref="BehaviorComponent"/>.
			/// </remarks>
			NodeUpdate = 4,

			/// <summary>
			/// Describes cost of <see cref="Lime.NodeProcessor.Update(float)"/>.
			/// </summary>
			/// <remarks>
			/// The <see cref="TypeGuid"/> contains GUID of type derived from <see cref="Lime.NodeProcessor"/>.
			/// </remarks>
			NodeProcessor = 5,

			/// <summary>
			/// Describes cost of <see cref="BehaviorComponent.Update(float)"/>.
			/// </summary>
			/// <remarks>
			/// The <see cref="TypeGuid"/> contains GUID of type derived from <see cref="BehaviorComponent"/>.
			/// </remarks>
			BehaviorComponentUpdate = 6,

			/// <summary>
			/// Describes CPU usage to get a <see cref="RenderObject"/> which performed in update thread.
			/// </summary>
			/// <remarks>
			/// The <see cref="TypeGuid"/> contains GUID of type derived from <see cref="RenderObject"/>.
			/// </remarks>
			NodeRenderPreparation = 7,

			/// <summary>
			/// Rendering of a node can mean different processes in render thread.
			/// 1) Node rendering is a process of writing commands to command buffers
			///    and sending these buffers to the GPU for drawing the node.
			/// 2) Node rendering is the process of adding the node to a batch.
			///    In this case, several nodes will be drawn in one draw call.
			/// 3) Both of the above.
			/// </summary>
			/// <remarks>
			/// The <see cref="TypeGuid"/> contains GUID of type derived from <see cref="RenderObject"/>.
			/// </remarks>
			NodeRender = 8,

			/// <summary>
			/// Batch rendering is a process in render thread of writing commands to command
			/// buffers and sending these buffers to the GPU for drawing nodes in the batch.
			/// </summary>
			/// <remarks>
			/// The <see cref="TypeGuid"/> contains <see cref="RenderBatch{TVertex}"/> type GUID.
			/// </remarks>
			BatchRender = 9,

			/// <summary>
			/// previous update      previous render
			/// ▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀ ▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀
			///                current update     wait         current render
			///                ▀▀▀▀▀▀▀▀▀▀▀▀▀▀ ▀▀▀▀▀▀▀▀▀▀▀ ▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀
			/// </summary>
			/// <remarks>The <see cref="TypeGuid"/> contains <see cref="Guid.Empty"/>.</remarks>
			WaitForPreviousRendering = 10,

			/// <summary>
			/// It is used to describe the wait when acquiring the next buffer
			/// for rendering a frame on the GPU. If we observe latency at this
			/// point, then the GPU is not keeping up with the CPU.
			/// </summary>
			/// <remarks>The <see cref="TypeGuid"/> contains <see cref="Guid.Empty"/>.</remarks>
			WaitForAcquiringSwapchainBuffer = 11,

			/// <summary>
			/// To be able to find the node that owns the "CpuUsage" structure,
			/// we maintain a kind of shadow hierarchy of nodes, where objects
			/// are represented as structures with basic information about the
			/// node. Sometimes we need to remove garbage in this hierarchy.
			/// </summary>
			/// <remarks>The <see cref="TypeGuid"/> contains <see cref="Guid.Empty"/>.</remarks>
			ReferenceTableGarbageCollection = 12,

			/// <summary>
			/// The cost of executing code in Sync method body.
			/// </summary>
			/// <remarks>The <see cref="TypeGuid"/> contains <see cref="Guid.Empty"/>.</remarks>
			SyncBodyExecution = 13,

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
		}
	}
}

#endif // PROFILER
