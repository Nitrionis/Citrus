using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime;

namespace Tangerine.Core.Operations
{
	public class TimelineColumnRemove : Operation
	{
		public override bool IsChangingDocument => true;

		private readonly int column;
		private Dictionary<Node, int> keyRemoveAt = new Dictionary<Node, int>();
		private int markerRemoveAt = -1;

		private TimelineColumnRemove(int column)
		{
			this.column = column;
		}

		public static void Perform(int column)
		{
			Document.Current.History.Perform(new TimelineColumnRemove(column));
		}

		public class Processor : OperationProcessor<TimelineColumnRemove>
		{
			protected override void InternalRedo(TimelineColumnRemove op)
			{
				op.keyRemoveAt.Clear();
				if (Document.Current.Animation.IsLegacy) {
					foreach (var node in Document.Current.Container.Nodes) {
						RemoveTimelineColumn(op, node);
					}
				} else {
					var toProcessNodes = new List<Node>();
					toProcessNodes.AddRange(Document.Current.Animation.OwnerNode.Nodes);
					while (toProcessNodes.Count > 0) {
						var processNode = toProcessNodes[toProcessNodes.Count - 1];
						toProcessNodes.RemoveAt(toProcessNodes.Count - 1);
						RemoveTimelineColumn(op, processNode);
						if (!processNode.Animations.TryFind(Document.Current.AnimationId, out _)) {
							toProcessNodes.AddRange(processNode.Nodes);
						}
					}
				}
				var markers = Document.Current.Animation.Markers;
				if (markers.Count == 0) {
					return;
				}
				if (op.markerRemoveAt == -1) {
					var markersOccupied = new HashSet<int>();
					foreach (var m in markers) {
						markersOccupied.Add(m.Frame);
					}
					for (var i = op.column; ; ++i) {
						if (!markersOccupied.Contains(i)) {
							op.markerRemoveAt = i;
							break;
						}
					}
				}
				foreach (var m in markers) {
					if (m.Frame != 0 && m.Frame >= op.markerRemoveAt) {
						m.Frame -= 1;
					}
				}
			}

			protected override void InternalUndo(TimelineColumnRemove op)
			{
				if (Document.Current.Animation.IsLegacy) {
					foreach (var node in Document.Current.Container.Nodes) {
						ProcessNode(node);
					}
				} else {
					var toProcessNodes = new List<Node>();
					toProcessNodes.AddRange(Document.Current.Animation.OwnerNode.Nodes);
					while (toProcessNodes.Count > 0) {
						var processNode = toProcessNodes[toProcessNodes.Count - 1];
						toProcessNodes.RemoveAt(toProcessNodes.Count - 1);
						ProcessNode(processNode);
						if (!processNode.Animations.TryFind(Document.Current.AnimationId, out _)) {
							toProcessNodes.AddRange(processNode.Nodes);
						}
					}
				}
				void ProcessNode(Node node)
				{
					foreach (var animator in node.Animators.Where(i => i.AnimationId == Document.Current.AnimationId)) {
						foreach (var k in animator.ReadonlyKeys.Reverse()) {
							if (k.Frame >= op.keyRemoveAt[node]) {
								k.Frame += 1;
							}
						}
						animator.ResetCache();
					}
					node.Animators.Invalidate();
				}
				foreach (var m in Document.Current.Animation.Markers.Reverse()) {
					if (m.Frame >= op.markerRemoveAt) {
						m.Frame += 1;
					}
				}
			}

			private void RemoveTimelineColumn(TimelineColumnRemove op, Node node)
			{
				if (node.Animators.Count == 0) {
					return;
				}
				if (!op.keyRemoveAt.ContainsKey(node)) {
					var occupied = new HashSet<int>();
					foreach (var animator in node.Animators.Where(i => i.AnimationId == Document.Current.AnimationId)) {
						foreach (var k in animator.ReadonlyKeys) {
							occupied.Add(k.Frame);
						}
					}
					for (var i = op.column; ; ++i) {
						if (!occupied.Contains(i)) {
							op.keyRemoveAt[node] = i;
							break;
						}
					}
				}
				foreach (var animator in node.Animators.Where(i => i.AnimationId == Document.Current.AnimationId)) {
					foreach (var k in animator.ReadonlyKeys) {
						if (k.Frame != 0 && k.Frame >= op.keyRemoveAt[node]) {
							k.Frame -= 1;
						}
					}
					animator.ResetCache();
				}
				node.Animators.Invalidate();
			}
		}
	}
}
