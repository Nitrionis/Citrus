using System.Collections.Generic;

namespace Lime.Graphics.Platform
{
	public class RenderBatchProfiler
	{
		public static int FullSavedByBatching { get; protected set; }
		public static int SceneSavedByBatching { get; protected set; }

		public static void Reset()
		{
			FullSavedByBatching = 0;
			SceneSavedByBatching = 0;
		}

		public bool IsPartOfScene { get; protected set; }
		public List<object> DrawCallsOwners { get; protected set; } = new List<object>();

		public void ProcessNode(object node, object manager)
		{
			if (node == null) {
				node = null;
			}
			DrawCallsOwners.Add(node);
			IsPartOfScene |=
				node == null ||
				SceneProfilingInfo.NodeManager == null || 
				ReferenceEquals(SceneProfilingInfo.NodeManager, manager);
		}
	}
}
