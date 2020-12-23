#if PROFILER

using System;
using Lime;

namespace Tangerine.UI.Charts
{
	internal interface IChartScaleProvider
	{
		float GetScale(int lineIndex);
	}
	
	internal struct Line
	{
		public string Label;
		public Color4 Color;
		public Vector2 Start;
		public Vector2 End;
	}
	
	internal class LinesContainer : Widget
	{
		private readonly Line[][] lines;
		private readonly float[][] scales;
		private readonly IChartScaleProvider scaleProvider;

		private int writeTargetIndex;

		public Line[] Lines => lines[writeTargetIndex];

		public LinesContainer(int linesCount, IChartScaleProvider scaleProvider)
		{
			lines = new Line[2][];
			lines[0] = new Line[linesCount];
			lines[1] = new Line[linesCount];
			scales = new float[2][];
			scales[0] = new float[linesCount];
			scales[1] = new float[linesCount];
			this.scaleProvider = scaleProvider;
			Presenter = new LinesPresenter();
			Updating += delta => {
				int srcIndex = writeTargetIndex;
				int dstIndex = 1 - writeTargetIndex;
				Array.Copy(lines[srcIndex], lines[dstIndex], linesCount);
				Array.Copy(scales[srcIndex], scales[dstIndex], linesCount);
				writeTargetIndex = dstIndex;
			};
		}

		private class LinesPresenter : IPresenter
		{
			public Lime.RenderObject GetRenderObject(Node node)
			{
				var ro = RenderObjectPool<RenderObject>.Acquire();
				ro.CaptureRenderState(node.AsWidget);
				var linesContainer = (LinesContainer)node;
				ro.Lines = linesContainer.lines[linesContainer.writeTargetIndex];
				ro.Scales = linesContainer.scales[linesContainer.writeTargetIndex];
				for (int i = 0; i < ro.Lines.Length; i++) {
					ro.Scales[i] = linesContainer.scaleProvider.GetScale(i);
				}
				ro.ContainerSize = node.AsWidget.Size;
				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class RenderObject : WidgetRenderObject
			{
				public Line[] Lines;
				public float[] Scales;
				public Vector2 ContainerSize;
				
				public override void Render()
				{
					PrepareRenderState();
					for (int i = 0; i < Lines.Length; i++) {
						var line = Lines[i];
						var scale = Scales[i];
						var a = new Vector2(line.Start.X, ContainerSize.Y - line.Start.Y * scale);
						var b = new Vector2(line.End.X, ContainerSize.Y - line.End.Y * scale);
						if (
							((a.X >= 0 & a.X <= ContainerSize.X) & (a.Y >= 0 & a.Y <= ContainerSize.Y)) ||
							((b.X >= 0 & b.X <= ContainerSize.X) & (b.Y >= 0 & b.Y <= ContainerSize.Y))
							)
						{
							Renderer.DrawLine(a, b, line.Color);
							if (!string.IsNullOrEmpty(line.Label)) {
								var position = a + new Vector2(4, -4);
								Renderer.DrawTextLine(position, line.Label, 16f, line.Color, 0);
							}
						}
					}
				}
			}
		}
	}
}

#endif // PROFILER