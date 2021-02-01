#if PROFILER

using System;
using Lime;
using Lime.Profiler;

namespace Tangerine.UI.Timelines
{
	internal static class CpuTimelineColorSet
	{
		public const int BreakReasonPreservedBitsCount = 1;
		
		private static ColorTheme.ProfilerColors profilerColors = ColorTheme.Current.Profiler;

		private static Color4[] defaultModeColors;
		private static Color4[] breakReasonsModeColors;

		public static Color4[] GetDefaultModeColors() =>
			(defaultModeColors = defaultModeColors ?? new[] {
				profilerColors.TimelineUnselectedTasks, 
				profilerColors.TimelineAnimationTasks,
				profilerColors.TimelineUpdateTasks, 
				profilerColors.TimelineGestureTasks,
				profilerColors.TimelineRenderPreparationTasks, 
				profilerColors.TimelineNodeRenderTasks,
				profilerColors.TimelineBatchRenderTasks, 
				profilerColors.TimelineWaitTasks,
				profilerColors.TimelineAudioSystemTasks, 
				profilerColors.TimelineDeserializationTasks,
				profilerColors.TimelineRunPendingActionsTasks, 
				Color4.Red
			});


		public static Color4[] GetBatchBreakReasonsColors()
		{
			if (breakReasonsModeColors == null) {
				var colors = new Color4[ColorIndicesPack.MaxColorsCount];
				colors[0] = profilerColors.TimelineUnselectedTasks;
				uint GetBitIndex(CpuUsage.Reasons reason) {
					uint data = (uint) reason & CpuUsage.BatchBreakReasons.BitMask;
					data >>= CpuUsage.BatchBreakReasons.StartBitIndex - BreakReasonPreservedBitsCount;
					return data;
				}
				colors[0] = profilerColors.TimelineUnselectedTasks;
				colors[GetBitIndex(CpuUsage.Reasons.BatchBreakNullLastBatch)] =
					profilerColors.TimelineBatchBreakNullLastBatch;
				colors[GetBitIndex(CpuUsage.Reasons.BatchBreakDifferentMaterials)] =
					profilerColors.TimelineBatchBreakDifferentMaterials;
				colors[GetBitIndex(CpuUsage.Reasons.BatchBreakMaterialPassCount)] =
					profilerColors.TimelineBatchBreakMaterialPassCount;
				colors[GetBitIndex(CpuUsage.Reasons.BatchBreakVertexBufferOverflow)] =
					profilerColors.TimelineBatchBreakVertexBufferOverflow;
				colors[GetBitIndex(CpuUsage.Reasons.BatchBreakIndexBufferOverflow)] =
					profilerColors.TimelineBatchBreakIndexBufferOverflow;
				colors[GetBitIndex(CpuUsage.Reasons.BatchBreakDifferentAtlasOne)] =
					profilerColors.TimelineBatchBreakDifferentAtlasOne;
				colors[GetBitIndex(CpuUsage.Reasons.BatchBreakDifferentAtlasTwo)] =
					profilerColors.TimelineBatchBreakDifferentAtlasTwo;
				breakReasonsModeColors = colors;
			}
			return breakReasonsModeColors;
		}


		public static uint[] GetUsageReasonColorIndices()
		{
			var usageReasonToColors = new uint[(int) CpuUsage.Reasons.MaxReasonIndex + 1];
			var colors = GetDefaultModeColors();
			uint IndexOf(Color4 color) {
				int index = Array.IndexOf(colors, color);
				return (uint) (index != -1 ? index : throw new System.Exception("Profiler: Color not found!"));
			}
			for (int i = 0; i < usageReasonToColors.Length; i++) {
				usageReasonToColors[i] = IndexOf(Color4.Red);
			}
			usageReasonToColors[(int) CpuUsage.Reasons.FullUpdate] =
				IndexOf(profilerColors.TimelineUnselectedTasks);
			usageReasonToColors[(int) CpuUsage.Reasons.SyncBodyExecution] =
				IndexOf(profilerColors.TimelineUnselectedTasks);
			usageReasonToColors[(int) CpuUsage.Reasons.FullRender] =
				IndexOf(profilerColors.TimelineUnselectedTasks);
			usageReasonToColors[(int) CpuUsage.Reasons.NodeAnimation] =
				IndexOf(profilerColors.TimelineAnimationTasks);
			usageReasonToColors[(int) CpuUsage.Reasons.NodeUpdate] =
				IndexOf(profilerColors.TimelineUpdateTasks);
			usageReasonToColors[(int) CpuUsage.Reasons.NodeProcessor] =
				IndexOf(profilerColors.TimelineUpdateTasks);
			usageReasonToColors[(int) CpuUsage.Reasons.BehaviorComponentUpdate] =
				IndexOf(profilerColors.TimelineUpdateTasks);
			usageReasonToColors[(int) CpuUsage.Reasons.NodeDeserialization] =
				IndexOf(profilerColors.TimelineDeserializationTasks);
			usageReasonToColors[(int) CpuUsage.Reasons.LoadExternalScenes] =
				IndexOf(profilerColors.TimelineDeserializationTasks);
			usageReasonToColors[(int) CpuUsage.Reasons.NodeRenderPreparation] =
				IndexOf(profilerColors.TimelineRenderPreparationTasks);
			usageReasonToColors[(int) CpuUsage.Reasons.NodeRender] =
				IndexOf(profilerColors.TimelineNodeRenderTasks);
			usageReasonToColors[(int) CpuUsage.Reasons.BatchRender] =
				IndexOf(profilerColors.TimelineBatchRenderTasks);
			usageReasonToColors[(int) CpuUsage.Reasons.WaitForPreviousRendering] =
				IndexOf(profilerColors.TimelineWaitTasks);
			usageReasonToColors[(int) CpuUsage.Reasons.WaitForAcquiringSwapchainBuffer] =
				IndexOf(profilerColors.TimelineWaitTasks);
			usageReasonToColors[(int) CpuUsage.Reasons.ReferenceTableGarbageCollection] =
				IndexOf(Color4.Red);
			usageReasonToColors[(int) CpuUsage.Reasons.ProfilerDatabaseResizing] =
				IndexOf(Color4.Red);
			usageReasonToColors[(int) CpuUsage.Reasons.RunScheduledActions] =
				IndexOf(profilerColors.TimelineRunPendingActionsTasks);
			usageReasonToColors[(int) CpuUsage.Reasons.RunPendingActionsOnRendering] =
				IndexOf(profilerColors.TimelineRunPendingActionsTasks);
			usageReasonToColors[(int) CpuUsage.Reasons.AudioSystemUpdate] =
				IndexOf(profilerColors.TimelineAudioSystemTasks);
			usageReasonToColors[(int) CpuUsage.Reasons.IssueCommands] =
				IndexOf(profilerColors.TimelineGestureTasks);
			usageReasonToColors[(int) CpuUsage.Reasons.ProcessCommands] =
				IndexOf(profilerColors.TimelineGestureTasks);
			return usageReasonToColors;
		}
	}
}

#endif // PROFILER
