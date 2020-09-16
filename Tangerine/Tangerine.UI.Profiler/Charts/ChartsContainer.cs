using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Lime;

namespace Tangerine.UI.Charts
{
	/// <summary>
	/// Common interface for all charts.
	/// </summary>
	internal interface IChart
	{
		/// <summary>
		/// Slot index in a charts group.
		/// </summary>
		int SlotIndex { get; }

		/// <summary>
		/// Indicates the visibility of the chart.
		/// </summary>
		bool Visible { get; }

		/// <summary>
		/// Each charts group supports up to 16 colors.
		/// </summary>
		int ColorIndex { get; }
	}

	/// <summary>
	/// Common code for some charts.
	/// </summary>
	internal class ChartBase : IChart
	{
		private int slotIndex = -1;

		public int SlotIndex
		{
			get { return slotIndex; }
			set {
				if (slotIndex != -1) {
					throw new NotImplementedException();
				}
				slotIndex = value;
			}
		}

		public bool Visible { get; set; }

		public int ColorIndex { get; set; }
	}

	/// <summary>
	/// Contains several charts.
	/// </summary>
	internal interface IChartsGroup
	{
		/// <summary>
		/// The widget that is responsible for drawing this group of charts.
		/// </summary>
		Widget Container { get; }

		/// <summary>
		/// Provides access to the charts.
		/// </summary>
		ReadOnlyCollection<IChart> Charts { get; }

		/// <summary>
		/// Invoked when chart visibility changed.
		/// </summary>
		event Action<IChart> ChartVisibleChanged;

		/// <summary>
		/// Available colors for charts.
		/// </summary>
		ReadOnlyCollection<Color4> Colors { get; }

		/// <summary>
		/// Allows you to turn the display of a chart on and off.
		/// </summary>
		void SetVisibleFor(IChart chart, bool visible);

		/// <summary>
		/// Allows you to change color of a chart.
		/// </summary>
		/// <param name="colorIndex">Index of an item at <see cref="Colors"/>.</param>
		void SetColorFor(IChart chart, int colorIndex);
	}

	/// <summary>
	/// Performed in the update and is responsible for building a mesh for a group of charts.
	/// </summary>
	internal interface IChartsGroupMeshBuilder
	{
		/// <summary>
		/// A reference to a vertex buffer that was or will be built during
		/// this or one of the previous updates and is guaranteed not to change
		/// during rendering, which will be caused by changes in this update.
		/// The link may change in every update.
		/// </summary>
		Vector3[] Vertices { get; }

		/// <summary>
		/// First visible vertex index for next rendering.
		/// </summary>
		int FirstVisibleVertex { get; }

		/// <summary>
		/// Count of visible vertices for next rendering.
		/// </summary>
		int VisibleVertexCount { get; }

		/// <summary>
		/// Mesh dirty flags for next rendering.
		/// </summary>
		MeshDirtyFlags MeshDirtyFlags { get; }
	}

	/// <summary>
	/// Stores several groups of сharts.
	/// </summary>
	internal class ChartsContainer : Widget
	{
		private readonly WidgetFlatFillPresenter presenter;

		/// <summary>
		/// Background color for charts.
		/// </summary>
		public Color4 BackgroundColor
		{
			get => presenter.Color;
			set => presenter.Color = value;
		}

		public ChartsContainer(IEnumerable<IChartsGroup> groups)
		{
			presenter = new WidgetFlatFillPresenter(Color4.Black);
			CompoundPresenter.Add(presenter);
			Layout = new StackLayout();
			foreach (var group in groups) {
				AddNode(group.Container);
			}
		}
	}

	internal static class ChartsCommon
	{
		public class Presenter : IPresenter
		{
			private readonly Mesh<Vector3> mesh;
			private readonly ChartsMaterial material;
			private readonly IChartsGroup charts;
			private readonly IChartsGroupMeshBuilder meshBuilder;

			public Presenter(IChartsGroup charts, IChartsGroupMeshBuilder meshBuilder)
			{
				this.charts = charts;
				this.meshBuilder = meshBuilder;
				mesh = new Mesh<Vector3> {
					Indices = new ushort[0],
					Vertices = null, // because the mesh is rebuilt in the update
					Topology = PrimitiveTopology.TriangleStrip,
					AttributeLocations = ChartsMaterial.ShaderProgram.MeshAttribLocations,
				};
				material = new ChartsMaterial();
				for (int i = 0; i < charts.Colors.Count; i++) {
					material.Colors[i] = charts.Colors[i].ToVector4();
				}
			}

			public RenderObject GetRenderObject(Node node)
			{
				var ro = RenderObjectPool<ChartsRenderObject>.Acquire();
				ro.CaptureRenderState(charts.Container);
				ro.Material = material;
				ro.Mesh = mesh;
				ro.Vertices = meshBuilder.Vertices;
				ro.FirstVisibleVertex = meshBuilder.FirstVisibleVertex;
				ro.VisibleVerticesCount = meshBuilder.VisibleVertexCount;
				ro.MeshDirtyFlags = meshBuilder.MeshDirtyFlags;
				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class ChartsRenderObject : WidgetRenderObject
			{
				public ChartsMaterial Material;
				public Mesh<Vector3> Mesh;
				public Vector3[] Vertices;
				public int FirstVisibleVertex;
				public int VisibleVerticesCount;
				public MeshDirtyFlags MeshDirtyFlags;

				public override void Render()
				{
					Material.Apply(0);
					Mesh.Vertices = Vertices;
					Mesh.DirtyFlags |= MeshDirtyFlags;
					Mesh.Draw(0, VisibleVerticesCount);
				}
			}
		}
	}
}
