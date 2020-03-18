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

		protected bool isPartOfScene;
		protected List<object> drawCallsOwners = new List<object>();

		public void ProcessNode(object node, object manager)
		{
			if (node == null) {
				node = null;
			}
			drawCallsOwners.Add(node);
			isPartOfScene |=
				node == null ||
				ProfilingInfo.SceneNodeManager == null || 
				ReferenceEquals(ProfilingInfo.SceneNodeManager, manager);
		}
	}
}
