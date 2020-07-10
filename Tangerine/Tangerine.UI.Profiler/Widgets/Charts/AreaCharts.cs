using System;
using Lime;

namespace Tangerine.UI.Charts
{
	internal class AreaCharts : ChartsContainer
	{
		private float maxAccumulatedHeight;
		private float[] accumulatedHeights;

		public AreaCharts(Parameters parameters) : base(parameters)
		{
			accumulatedHeights = new float[parameters.ControlPointsCount];
			Presenter = new AreaChartsPresenter(parameters.ChartsColors, this);
		}

		private void ResetAccumulatedHeights()
		{
			maxAccumulatedHeight = 0;
			for (int i = 0; i < accumulatedHeights.Length; i++) {
				maxAccumulatedHeight = Mathf.Max(maxAccumulatedHeight, accumulatedHeights[i]);
				accumulatedHeights[i] = 0;
			}
		}

		private void RebuildChartsSlice(int sliceIndex)
		{

		}

		protected override void RebuildFullMesh(Mesh<Vector3> mesh)
		{
			float scaleCoefficient = ScaleCoefficient;
			int vertexIndex = Line.VerticesCount * customLinesBuffers.Length;
			int parity = 0;
			foreach (var chart in Charts) {
				if (chart.IsVisible) {
					int step = (1 - parity) * 2 - 1;
					int start = parity * (chart.Points.Capacity - 2);
					int end = (1 - parity) * (chart.Points.Capacity - 1) - parity;
					for (int i = start; i != end; i += step) {
						float point = chart.Points.GetItem(i + 1);
						float ah = accumulatedHeights[i];
						accumulatedHeights[i] += point;
						float x = i * ControlPointsSpacing;
						float y0 = Height - (ah + point * parity) * scaleCoefficient;
						float y1 = Height - (ah + point * (1 - parity)) * scaleCoefficient;
						mesh.Vertices[vertexIndex++] = new Vector3(x, y0, chart.ColorIndex);
						mesh.Vertices[vertexIndex++] = new Vector3(x, y1, chart.ColorIndex);
					}
					parity = (parity + 1) % 2;
				}
			}
			//mesh.DirtyFlags |= MeshDirtyFlags.Vertices;
		}



		protected override int GetMaxVertexBufferCapacity()
		{
			throw new NotImplementedException();
		}

		protected override int GetMaxIndexBufferCapacity()
		{
			throw new NotImplementedException();
		}

		public float ScaleCoefficient => Height * 0.9f / Mathf.Max(chartsMaxValue, 0.01f);

		private class AreaChartsPresenter : ChartsPresenter
		{
			public AreaChartsPresenter(Color4[] colors, ChartsContainer container) : base(colors, container)
			{

			}

			protected override ChartsPresenter.RenderObject AcquireRenderObject()
			{
				return RenderObjectPool<RenderObject>.Acquire();
			}

			protected new class RenderObject : ChartsPresenter.RenderObject
			{
				protected override void DrawCharts()
				{
					throw new NotImplementedException();
				}
			}
		}
	}
}
