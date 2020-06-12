using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lime;
using Lime.Graphics.Platform.Profiling;

namespace Tangerine.UI.Timeline
{
	internal static class Rectangle
	{
		public const int VertexCount = 4;
		public const int IndexCount = 6;

		public static void WriteVerticesTo(Mesh<TimelineMaterial.Vertex> dstMesh, int dstOffset, Vector2 position, Vector2 size)
		{
			dstMesh.Vertices[dstOffset + 0].Position = new Vector4(position.X, position.Y, 0, 1);
			dstMesh.Vertices[dstOffset + 1].Position = new Vector4(position.X, position.Y + size.Y, 0, 1);
			dstMesh.Vertices[dstOffset + 2].Position = new Vector4(position.X + size.X, position.Y + size.Y, 0, 1);
			dstMesh.Vertices[dstOffset + 3].Position = new Vector4(position.X + size.X, position.Y, 0, 1);
		}

		public static void WriteColorsTo(Mesh<TimelineMaterial.Vertex> dstMesh, int dstOffset, Color4 color)
		{
			dstMesh.Vertices[dstOffset + 0].Color = color;
			dstMesh.Vertices[dstOffset + 1].Color = color;
			dstMesh.Vertices[dstOffset + 2].Color = color;
			dstMesh.Vertices[dstOffset + 3].Color = color;
		}

		public static void WriteIndicesTo(Mesh<TimelineMaterial.Vertex> dstMesh, int dstOffset, int startVertex)
		{
			dstMesh.Indices[dstOffset + 0] = (ushort)(startVertex + 0);
			dstMesh.Indices[dstOffset + 1] = (ushort)(startVertex + 1);
			dstMesh.Indices[dstOffset + 2] = (ushort)(startVertex + 2);
			dstMesh.Indices[dstOffset + 3] = (ushort)(startVertex + 0);
			dstMesh.Indices[dstOffset + 4] = (ushort)(startVertex + 2);
			dstMesh.Indices[dstOffset + 5] = (ushort)(startVertex + 3);
		}
	}

	internal interface IItem
	{
		ITimePeriod TimePeriod { get; }
		Vector2 Position { get; }
		int VertexCount { get; }
		int IndexCount { get; }
		void SceneFilterChanged(bool value);
		void TargetNodeChanged(Regex regex);
		void UpdateMeshPositionsSelfSegment();
		void RebuildMeshIndicesSelfSegment(int dstOffset);
	}

	internal abstract class TimelineContainer<Item> : Widget where Item : IItem
	{
		protected const float ItemTopMargin = 2;
		protected const float ItemHeight = 20;

		private readonly TimelinePresenter presenter;
		protected readonly Mesh<TimelineMaterial.Vertex> mesh;

		private TimePeriod visibleTimePeriod;

		protected float MicrosecondsPerPixel;

		protected List<Item> items;

		/// <summary>
		/// Used to search for free space to visualize the item.
		/// </summary>
		private readonly List<uint> freeSpaceOfLines = new List<uint>();

		/// <summary>
		/// Timeline with timestamps above container elements.
		/// </summary>
		protected readonly TimelineRuler ruler;

		/// <summary>
		/// Widget representing time intervals.
		/// </summary>
		protected readonly Widget container;
		protected readonly ThemedScrollView horizontalScrollView;
		protected readonly ThemedScrollView verticalScrollView;

		private float previousHorizontalScrollPosition;

		public Action<Item> TimePeriodSelected;

		private bool isSceneOnly;

		public bool IsSceneOnly
		{
			get { return isSceneOnly; }
			set
			{
				isSceneOnly = value;
				foreach (var item in items) {
					item.SceneFilterChanged(value);
				}
				mesh.DirtyFlags |= MeshDirtyFlags.Vertices;
			}
		}

		private Regex regexNodeFilter;

		public Regex RegexNodeFilter
		{
			get { return regexNodeFilter; }
			set
			{
				regexNodeFilter = value;
				foreach (var item in items) {
					item.TargetNodeChanged(value);
				}
				mesh.DirtyFlags |= MeshDirtyFlags.Vertices;
			}
		}

		protected TimelineContainer()
		{
			Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Profiler.TimelineBackground);
			Layout = new VBoxLayout();
			MinMaxHeight = 100;

			Input.AcceptMouseBeyondWidget = false;
			Input.AcceptMouseThroughDescendants = true;

			items = new List<Item>();

			ruler = new TimelineRuler(10, 10) {
				Anchors = Anchors.LeftRight,
				MinMaxHeight = 32,
				Offset = 0,
				TimestampsColor = ColorTheme.Current.Profiler.TimelineRulerAndText
			};

			mesh = new Mesh<TimelineMaterial.Vertex> {
				Indices = new ushort[] { 0 },
				Vertices = new[] { new TimelineMaterial.Vertex() },
				Topology = PrimitiveTopology.TriangleList,
				AttributeLocations = TimelineMaterial.ShaderProgram.MeshAttribLocations
			};

			presenter = new TimelinePresenter(mesh);
			container = new Widget {
				Id = "Profiler timeline content",
				Presenter = presenter,
			};

			horizontalScrollView = new ThemedScrollView(ScrollDirection.Horizontal) {
				Anchors = Anchors.LeftRightTopBottom,
				HitTestTarget = true,
				HitTestMethod = HitTestMethod.BoundingRect,
				Clicked = () => OnContainerClicked()
			};
			horizontalScrollView.Content.Layout = new HBoxLayout();

			verticalScrollView = new ThemedScrollView(ScrollDirection.Vertical) {
				Anchors = Anchors.LeftRightTopBottom
			};
			verticalScrollView.Content.Layout = new VBoxLayout();

			horizontalScrollView.Content.AddNode(verticalScrollView);
			verticalScrollView.Content.AddNode(container);

			horizontalScrollView.Content.Tasks.Insert(0, new Task(HorizontalScrollTask()));
			verticalScrollView.Content.Tasks.Insert(0, new Task(VerticalScrollTask()));

			Tasks.Add(ScaleScrollTask);

			AddNode(ruler);
			AddNode(horizontalScrollView);
		}

		protected abstract float CalculateHistoryWidth();

		private void OnContainerClicked()
		{
			var position = horizontalScrollView.LocalMousePosition() + new Vector2(horizontalScrollView.ScrollPosition, 0);
			float timestamp = (position.X - ruler.SmallStep) * MicrosecondsPerPixel;
			foreach (var item in items) {
				if (
					item.TimePeriod.Start <= timestamp && timestamp <= item.TimePeriod.Finish &&
					item.Position.Y <= position.Y && position.Y <= item.Position.Y + ItemHeight
					) {
					TimePeriodSelected?.Invoke(item);
					break;
				}
			}
		}

		/// <returns>Visible items count</returns>
		protected void RebuildIndexBuffer()
		{
			int visibleIndexCount = 0;
			foreach (var item in items) {
				if (IsItemVisible(item)) {
					item.RebuildMeshIndicesSelfSegment(visibleIndexCount);
					visibleIndexCount += item.IndexCount;
				}
			}
			presenter.IndexCount = visibleIndexCount;
			mesh.DirtyFlags |= MeshDirtyFlags.Indices;
		}

		protected void RebuildVertexBuffer()
		{
			freeSpaceOfLines.Clear();
			foreach (var item in items) {
				item.UpdateMeshPositionsSelfSegment();
			}
			mesh.DirtyFlags |= MeshDirtyFlags.Vertices;
		}

		protected bool IsItemVisible(Item item) =>
			visibleTimePeriod.Start < item.TimePeriod.Finish &&
			item.TimePeriod.Start < visibleTimePeriod.Finish;

		private void RecalculateVisibleTimePeriod() =>
			visibleTimePeriod = new TimePeriod(
				(uint)(Math.Max(0, horizontalScrollView.ScrollPosition - ruler.SmallStep) * MicrosecondsPerPixel),
				(uint)((horizontalScrollView.ScrollPosition + horizontalScrollView.Width) * MicrosecondsPerPixel));

		protected void RecheckItemVisibility()
		{
			RecalculateVisibleTimePeriod();
			RebuildIndexBuffer();
		}

		protected void UpdateHistorySize()
		{
			float pcw = container.Width;
			float ncw = CalculateHistoryWidth();
			float nch = freeSpaceOfLines.Count * (ItemHeight + ItemTopMargin) + ItemTopMargin;
			float hsvp = horizontalScrollView.ScrollPosition;

			var size = new Vector2(ncw, nch);
			container.MinMaxSize = size;
			container.Size = size;
			verticalScrollView.MinMaxWidth = Mathf.Max(ncw, horizontalScrollView.Width);

			float lmp = LocalMousePosition().X;
			float pos = Mathf.Clamp((lmp + hsvp) / pcw - lmp / ncw, 0, 1);
			horizontalScrollView.ScrollPosition = pos * ncw;
		}

		protected Vector2 AcquirePosition(ITimePeriod period)
		{
			int lineIndex = -1;
			for (int i = 0; i < freeSpaceOfLines.Count; i++) {
				if (freeSpaceOfLines[i] < period.Start) {
					lineIndex = i;
					break;
				}
			}
			if (lineIndex == -1) {
				lineIndex = freeSpaceOfLines.Count;
				freeSpaceOfLines.Add(period.Finish);
			} else {
				freeSpaceOfLines[lineIndex] = period.Finish;
			}
			return new Vector2(
				ruler.SmallStep + period.Start / MicrosecondsPerPixel,
				ItemHeight * lineIndex + ItemTopMargin * (lineIndex + 1));
		}

		protected void ResetContainer()
		{
			MicrosecondsPerPixel = 1.0f;
			ruler.RulerScale = 1.0f;
			freeSpaceOfLines.Clear();
		}

		private IEnumerator<object> ScaleScrollTask()
		{
			while (true) {
				if (
					Input.IsKeyPressed(Key.Control) &&
					(Input.WasKeyPressed(Key.MouseWheelDown) || Input.WasKeyPressed(Key.MouseWheelUp))
					) {
					float microsecondsPerPixel = MicrosecondsPerPixel;
					microsecondsPerPixel += Input.WheelScrollAmount / 1200;
					microsecondsPerPixel = Mathf.Clamp(microsecondsPerPixel, 0.2f, 10f);
					ruler.RulerScale = microsecondsPerPixel;
					if (MicrosecondsPerPixel != microsecondsPerPixel) {
						MicrosecondsPerPixel = microsecondsPerPixel;
						UpdateHistorySize();
						RebuildVertexBuffer();
						RecheckItemVisibility();
					}
				}
				yield return null;
			}
		}

		private IEnumerator<object> HorizontalScrollTask()
		{
			while (true) {
				bool isHorizontalMode = !Input.IsKeyPressed(Key.Shift) && !Input.IsKeyPressed(Key.Control);
				horizontalScrollView.Behaviour.CanScroll = isHorizontalMode;
				verticalScrollView.Behaviour.StopScrolling();
				ruler.Offset = horizontalScrollView.ScrollPosition;
				if (previousHorizontalScrollPosition != horizontalScrollView.ScrollPosition) {
					previousHorizontalScrollPosition = horizontalScrollView.ScrollPosition;
					RecheckItemVisibility();
				}
				yield return null;
			}
		}

		private IEnumerator<object> VerticalScrollTask()
		{
			while (true) {
				bool isVerticalMode = Input.IsKeyPressed(Key.Shift) && !Input.IsKeyPressed(Key.Control);
				verticalScrollView.Behaviour.CanScroll = isVerticalMode;
				horizontalScrollView.Behaviour.StopScrolling();
				yield return null;
			}
		}

		protected class TimePeriodComparer<T> : IComparer<T> where T : ITimePeriod
		{
			public int Compare(T a, T b)
			{
				uint value = a.Start - b.Start;
				return value != 0 ? (int)value : (int)(b.Finish - a.Finish);
			}
		}

		protected class TimePeriod : ITimePeriod
		{
			public uint Start { get; set; }
			public uint Finish { get; set; }

			public TimePeriod(uint start, uint finish)
			{
				Start = start;
				Finish = finish;
			}
		}

		private class TimelinePresenter : IPresenter
		{
			private readonly TimelineMaterial material;
			private readonly Mesh<TimelineMaterial.Vertex> mesh;

			public int IndexCount;

			public TimelinePresenter(Mesh<TimelineMaterial.Vertex> mesh)
			{
				this.mesh = mesh;
				material = new TimelineMaterial();
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args)
			{
				//bool value = ((Widget)node).BoundingRectHitTest(args.Point);
				//Debug.Write(value);
				//return value;
				return false;
			}

			public Lime.RenderObject GetRenderObject(Node node)
			{
				var ro = RenderObjectPool<RenderObject>.Acquire();
				ro.CaptureRenderState((Widget)node);
				ro.Material = material;
				ro.Mesh = mesh;
				ro.IndexCount = IndexCount;
				return ro;
			}

			public IPresenter Clone() => (IPresenter)MemberwiseClone();

			private class RenderObject : WidgetRenderObject
			{
				public TimelineMaterial Material;
				public Mesh<TimelineMaterial.Vertex> Mesh;
				public int IndexCount;

				public override void Render()
				{
					PrepareRenderState();
					Renderer.MainRenderList.Flush();
					Material.Matrix = Renderer.FixupWVP((Matrix44)LocalToWorldTransform * Renderer.ViewProjection);
					Material.Apply(0);
#if !LIME_PROFILER
					Mesh.DrawIndexed(0, IndexCount);
#else
					Mesh.DrawIndexed(0, IndexCount, 0, ProfilingInfo.Acquire(Material, 0));
#endif
				}
			}
		}
	}
}
