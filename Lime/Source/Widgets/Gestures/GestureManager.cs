using System.Collections.Generic;
using System.Linq;
using Lime.Profilers;

namespace Lime
{
	public class GestureManager
	{
		protected readonly WidgetContext context;
		private readonly List<Gesture> activeGestures = new List<Gesture>();
		private Node activeNode;
		public int CurrentIteration { get; private set; }

		public GestureManager(WidgetContext context)
		{
			this.context = context;
		}

		public void Update(float delta)
		{
			CurrentIteration++;
			UpdateGestures(delta);
			if (context.NodeCapturedByMouse != activeNode) {
				activeNode = context.NodeCapturedByMouse;
				var doubleClickGestures = activeGestures.Where(g => g is DoubleClickGesture).Cast<DoubleClickGesture>().ToList();
				CancelGestures();
				if (activeNode != null) {
					activeGestures.AddRange(EnumerateGestures(activeNode));
					UpdateGestures(delta);
				} else {
					activeGestures.AddRange(doubleClickGestures);
				}
			}
		}

		private void UpdateGestures(float delta)
		{
			foreach (var gesture in activeGestures) {
#if LIME_PROFILER
				var usage = CpuProfiler.NodeCpuUsageStarted(gesture.Owner, CpuUsage.UsageReason.Gesture);
#endif
				if (gesture is ClickGesture cg) {
					cg.Deferred = false;
					foreach (var g in activeGestures) {
						cg.Deferred |= g is DragGesture dg && dg.ButtonIndex == cg.ButtonIndex;
					}
				}
				if (gesture.OnUpdate(delta)) {
					switch (gesture) {
						case DragGesture dg: {
							foreach (var g in activeGestures) {
								if (g == gesture) {
									continue;
								}
								var clickGesture = g as ClickGesture;
								if (clickGesture?.ButtonIndex == dg.ButtonIndex) {
									g.OnCancel();
								}
								if (dg.Exclusive) {
									(g as DragGesture)?.OnCancel();
								}
							}
							break;
						}
						case DoubleClickGesture dcg: {
							foreach (var g in activeGestures) {
								if (g != dcg) {
									g.OnCancel();
								}
							}
							break;
						}
					}
				}
#if LIME_PROFILER
				CpuProfiler.NodeCpuUsageFinished(usage);
#endif
			}
		}

		private void CancelGestures()
		{
			foreach (var g in activeGestures) {
				g.OnCancel();
			}
			activeGestures.Clear();
		}

		protected virtual IEnumerable<Gesture> EnumerateGestures(Node node)
		{
			var noMoreClicks = new bool[3];
			var noMoreDoubleClicks = new bool[3];
			for (; node != null; node = node.Parent) {
				if (node.HasGestures()) {
					foreach (var g in node.Gestures) {
						if (g is ClickGesture cg) {
							if (noMoreClicks[cg.ButtonIndex]) {
								continue;
							}
							noMoreClicks[cg.ButtonIndex] = true;
						}
						if (g is DoubleClickGesture dcg) {
							if (noMoreDoubleClicks[dcg.ButtonIndex]) {
								continue;
							}
							noMoreDoubleClicks[dcg.ButtonIndex] = true;
						}
						yield return g;
					}
				}
			}
		}
	}
}
