using System.Collections.Generic;

namespace Lime.Graphics.Platform.Profiling
{
	public class RenderBatchProfiler
	{
		public static int FullSavedByBatching { get; protected set; }
		public static int SceneSavedByBatching { get; protected set; }

		internal static void Reset()
		{
			FullSavedByBatching = 0;
			SceneSavedByBatching = 0;
		}
	}

	public class RenderBatchOwnersInfo : RenderBatchProfiler
	{
		public bool IsPartOfScene { get; private set; }
		public List<object> Owners { get; private set; } = new List<object>();

		public void ProcessNode(object node, object manager)
		{
			Owners.Add(node);
			IsPartOfScene |=
				node == null ||
				SceneProfilingInfo.NodeManager == null ||
				ReferenceEquals(SceneProfilingInfo.NodeManager, manager);
		}

		protected void ResetProfilingData()
		{
			IsPartOfScene = false;
			Owners = new List<object>();
		}
	}
}
