using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.Timeline
{
	public class ColumnCountUpdater : IDocumentUpdater
	{
		public void Update()
		{
			var timeline = Timeline.Instance;
			var rows = Document.Current.Rows;
			int maxColumn = 0;
			foreach (var row in rows) {
				var nodeData = row.Components.Get<Core.Components.NodeRow>();
				if (nodeData != null) {
					maxColumn = Math.Max(maxColumn, nodeData.Node.Animators.GetOverallDuration());
				}
			}
			var maxVisibleColumn = ((timeline.ScrollPos.X + timeline.Grid.Size.X) / TimelineMetrics.ColWidth).Ceiling();
			timeline.ColumnCount = Math.Max(maxColumn + 1, maxVisibleColumn);
		}
	}
}