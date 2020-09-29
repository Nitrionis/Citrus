using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AsyncTask = System.Threading.Tasks.Task;
using Lime;
using Tangerine.UI.Charts;

namespace Tangerine.UI.Timeline
{
	internal class Timeline : Widget
	{
		private readonly TimelineRuler ruler;
		private readonly Widget container;
		private readonly ThemedScrollView horizontalScrollView;
		private readonly ThemedScrollView verticalScrollView;

		private bool isMeshRebuildRequired;

		protected bool IsMeshRebuildRequired
		{
			set => isMeshRebuildRequired |= value;
		}

		private Vector2 cachedContainerSize;
		private float cachedHorizontalScrollPosition;
		private float cachedVerticalScrollPosition;
		private float cachedMicrosecondsPerPixel;

		private SpaceManager.Parameters spaceManagerParameters;

		protected Timeline(ContentPresenter contentPresenter)
		{
			Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Profiler.TimelineTasksBackground);
			Layout = new VBoxLayout();
			MinMaxHeight = 100;
			Input.AcceptMouseBeyondWidget = false;
			Input.AcceptMouseThroughDescendants = true;
			spaceManagerParameters = new SpaceManager.Parameters {
				ItemHeight = 20,
				ItemMargin = 2,
				MicrosecondsPerPixel = 1,
				HorizontalOffset = 0
			};
			ruler = new TimelineRuler(10, 10) {
				Anchors = Anchors.LeftRight,
				MinMaxHeight = 32,
				Offset = 0,
				TextColor = ColorTheme.Current.Profiler.TimelineRulerText,
				TimestampsColor = ColorTheme.Current.Profiler.TimelineRulerStep
			};
			container = new Widget {
				Id = "Profiler timeline content",
				Presenter = contentPresenter,
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
			AddNode(horizontalScrollView);
			AddNode(ruler);
			horizontalScrollView.Content.Tasks.Insert(0, new Task(HorizontalScrollTask()));
			verticalScrollView.Content.Tasks.Insert(0, new Task(VerticalScrollTask()));
			Tasks.Add(ScaleScrollTask);
			cachedContainerSize = Size;
			Updated += (delta) => {
				TimePeriod CalculateVisibleTimePeriod() {
					float scrollPosition = horizontalScrollView.ScrollPosition;
					return new TimePeriod {
						StartTime = (uint)(Math.Max(0, scrollPosition - ruler.SmallStep) * cachedMicrosecondsPerPixel),
						FinishTime = (uint)((scrollPosition + horizontalScrollView.Width) * cachedMicrosecondsPerPixel)
					};
				}
				isMeshRebuildRequired |= cachedContainerSize != Size;
				if (isMeshRebuildRequired) {
					isMeshRebuildRequired = false;
					cachedContainerSize = Size;
					var visibleTimePeriod = CalculateVisibleTimePeriod();
					// TODO run mesh rebuild
					throw new NotImplementedException();
				}
			};
			throw new NotImplementedException();
		}

		private IEnumerator<object> ScaleScrollTask()
		{
			while (true) {
				if (
					Input.IsKeyPressed(Key.Control) &&
					(Input.WasKeyPressed(Key.MouseWheelDown) || Input.WasKeyPressed(Key.MouseWheelUp))
					)
				{
					float microsecondsPerPixel = cachedMicrosecondsPerPixel;
					const float ScrollSpeed = 1200;
					microsecondsPerPixel += Input.WheelScrollAmount / ScrollSpeed;
					const float MinScale = 0.2f;
					const float MaxScale = 10f;
					microsecondsPerPixel = Mathf.Clamp(microsecondsPerPixel, MinScale, MaxScale);
					ruler.RulerScale = microsecondsPerPixel;
					if (cachedMicrosecondsPerPixel != microsecondsPerPixel) {
						cachedMicrosecondsPerPixel = microsecondsPerPixel;
						isMeshRebuildRequired = true;
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
				if (cachedHorizontalScrollPosition != horizontalScrollView.ScrollPosition) {
					cachedHorizontalScrollPosition = horizontalScrollView.ScrollPosition;
					isMeshRebuildRequired = true;
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
				if (cachedVerticalScrollPosition != horizontalScrollView.ScrollPosition) {
					cachedVerticalScrollPosition = horizontalScrollView.ScrollPosition;
					isMeshRebuildRequired = true;
				}
				yield return null;
			}
		}

		private void OnContainerClicked()
		{
			throw new NotImplementedException();
		}

		internal class ExecutionManager
		{
			private const int UndefinedHitTestTarget = -1;

			private readonly IMeshBuilder meshBuilder;
			private readonly int maxTasksCount;
			private readonly Queue<TaskRequest> waitingQueue;

			private Queue<System.Threading.Tasks.Task<TaskResult>> runningTasks;
			private Queue<System.Threading.Tasks.Task<TaskResult>> unusedQueue;
			private TimelineState lastTimelineState;
			private Result<int> hitTestResult;
			private TaskRequest currentTaskRequest = new TaskRequest();
			private IEnumerable<TimePeriod> timePeriods = Enumerable.Empty<TimePeriod>();
			private uint LastAssignedNoveltyIdentifier = 0;

			/// <summary>
			/// Represents all time intervals on the timeline including visible and invisible.
			/// Property assignment will cause synchronous waiting for all passwords!
			/// </summary>
			public IEnumerable<TimePeriod> TimePeriods
			{
				get { return timePeriods; }
				set {
					// Asynchronous tasks constantly read timeline intervals.
					// So we are waiting for all asynchronous tasks to complete.
					foreach (var task in runningTasks) {
						task.Wait();
					}
					runningTasks.Clear();
					timePeriods = value;
					currentTaskRequest.IsMeshRebuildRequired = true;
				}
			}

			/// <summary>
			/// Indicates whether any task is running.
			/// </summary>
			public bool IsRunning => runningTasks.Count > 0;

			/// <summary>
			/// Represents a reference to rendering resources that will not change at least during the next update.
			/// </summary>
			public Result<IRenderingResources> RenderingResources { get; private set; }

			/// <summary>
			/// Occurs when we click on a <see cref="TimePeriod"/>.
			/// </summary>
			/// <remarks>
			///	If there were several clicks, it can be called only for the last click.
			///	This event is deferred.
			/// </remarks>
			public event Action<int> TimePeriodSelected;

			public ExecutionManager(IMeshBuilder meshBuilder, TimelineState state, int maxParallelTasksCount = 2)
			{
				this.meshBuilder = meshBuilder;
				lastTimelineState = state;
				maxTasksCount = maxParallelTasksCount;
				runningTasks = new Queue<System.Threading.Tasks.Task<TaskResult>>(maxTasksCount);
				unusedQueue = new Queue<System.Threading.Tasks.Task<TaskResult>>(maxTasksCount);
			}

			public void TimelineUpdated()
			{
				foreach (var task in runningTasks) {
					if (task.IsCompleted) {
						if (task.Result.HasMeshRebuildingResult) {
							RenderingResources = SelectNewest(RenderingResources, task.Result.MeshRebuildingResult);
						}
						if (task.Result.HasHitTestResult) {
							hitTestResult = SelectNewest(hitTestResult, task.Result.HitTestResult);
						}
					} else {
						unusedQueue.Enqueue(task);
					}
				}
				runningTasks.Clear();
				void SwapTasksQueue() {
					var tmp = runningTasks;
					runningTasks = unusedQueue;
					unusedQueue = tmp;
				}
				SwapTasksQueue();
				if (!currentTaskRequest.IsEmpty) {
					waitingQueue.Enqueue(currentTaskRequest);
					currentTaskRequest = new TaskRequest();
				}
				if (waitingQueue.Count > 0) {
					// Iterating over tasks and removing meaningless.
					var finalRequest = new TaskRequest();
					foreach (var request in waitingQueue) {
						if (request.IsMeshRebuildRequired) {
							finalRequest.IsMeshRebuildRequired = true;
							finalRequest.MeshRebuildTimelineState = request.MeshRebuildTimelineState;
						}
						if (request.IsHitTestRequired) {
							finalRequest.IsHitTestRequired = true;
							finalRequest.HitTestTimelineState = request.HitTestTimelineState;
						}
					}
					waitingQueue.Clear();
					waitingQueue.Enqueue(finalRequest);
					if (runningTasks.Count < maxTasksCount) {
						// TODO try to run async task
						throw new NotImplementedException();
					}
				}
				if (hitTestResult.Value != UndefinedHitTestTarget) {
					TimePeriodSelected?.Invoke(hitTestResult.Value);
				}
			}

			public void RequestAsyncMeshRebuilding(TimelineState state)
			{
				currentTaskRequest.IsMeshRebuildRequired = true;
				currentTaskRequest.MeshRebuildTimelineState = state;
			}

			public void RequestAsyncHitTestCheck(TimelineState state)
			{
				currentTaskRequest.IsHitTestRequired = true;
				currentTaskRequest.HitTestTimelineState = state;
			}

			private static Result<T> SelectNewest<T>(Result<T> first, Result<T> second)
			{
				bool isFirstClemped = (long)second.NoveltyID - (long)first.NoveltyID > uint.MaxValue / 2;
				return (first.NoveltyID > second.NoveltyID || isFirstClemped) ? first : second;
			}

			public struct Result<ValueType>
			{
				public uint NoveltyID;
				public ValueType Value;
			}

			private struct TaskRequest
			{
				public bool IsMeshRebuildRequired;
				public bool IsHitTestRequired;

				public TimelineState MeshRebuildTimelineState;
				public TimelineState HitTestTimelineState;

				public bool IsEmpty => !IsMeshRebuildRequired && !IsHitTestRequired;
			}

			private struct TaskResult
			{
				/// <summary>
				/// Task novelty identifier.
				/// </summary>
				public uint NoveltyID;

				/// <summary>
				/// Represents new render resources.
				/// </summary>
				public Result<IRenderingResources> MeshRebuildingResult;

				/// <summary>
				/// Represents an <see cref="TimePeriod"/> index.
				/// </summary>
				public Result<int> HitTestResult;

				public bool IsEmpty =>
					MeshRebuildingResult.Value == null &&
					HitTestResult.Value == UndefinedHitTestTarget;

				public bool HasHitTestResult => HitTestResult.Value != UndefinedHitTestTarget;

				public bool HasMeshRebuildingResult => MeshRebuildingResult.Value == null;
			}
		}

		internal interface IMeshBuilder
		{
			
			IRenderingResources RebuildMesh(TimelineState state);
		}

		internal class SpaceManager
		{
			private readonly List<uint> freeSpaceOfLines;
			private Parameters parameters;

			public SpaceManager(Parameters parameters)
			{
				freeSpaceOfLines = new List<uint>();
				Reset(parameters);
			}

			public Vector2 AcquirePosition(TimePeriod period)
			{
				int lineIndex = -1;
				for (int i = 0; i < freeSpaceOfLines.Count; i++) {
					if (freeSpaceOfLines[i] < period.StartTime) {
						lineIndex = i;
						break;
					}
				}
				// Add extra time to make small intervals on the timeline more visible.
				uint finishTime = period.FinishTime +
					(uint)Math.Ceiling(parameters.MicrosecondsPerPixel);
				if (lineIndex == -1) {
					lineIndex = freeSpaceOfLines.Count;
					freeSpaceOfLines.Add(finishTime);
				} else {
					freeSpaceOfLines[lineIndex] = finishTime;
				}
				return new Vector2(
					parameters.HorizontalOffset + period.StartTime / parameters.MicrosecondsPerPixel,
					parameters.ItemHeight * lineIndex + parameters.ItemMargin * (lineIndex + 1));
			}

			public void Reset(Parameters parameters)
			{
				freeSpaceOfLines.Clear();
				this.parameters = parameters;
			}

			public struct Parameters
			{
				public float MicrosecondsPerPixel;
				public float HorizontalOffset;
				public float ItemMargin;
				public float ItemHeight;
			}
		}

		internal struct TimePeriod
		{
			/// <summary>
			/// Start time in microseconds.
			/// </summary>
			public uint StartTime;

			/// <summary>
			/// Finish time in microseconds.
			/// </summary>
			public uint FinishTime;
		}

		internal abstract class ContentPresenter : IPresenter
		{
			private readonly ChartsMaterial material;
			private readonly RectanglesMesh mesh;
			private readonly Color4[] colors;

			protected ContentPresenter(Color4[] colors)
			{
				this.colors = (Color4[])colors.Clone();
				material = new ChartsMaterial();
				for (int i = 0; i < this.colors.Length; i++) {
					material.Colors[i] = this.colors[i].ToVector4();
				}
				mesh = new RectanglesMesh();
			}

			/// <summary>
			/// Returns a link to <see cref="IRenderingResources"/> that will not change during the next render.
			/// </summary>
			protected abstract IRenderingResources GetProtectedResources();

			public virtual RenderObject GetRenderObject(Node node)
			{
				var ro = RenderObjectPool<TimelineRenderObject>.Acquire();
				ro.CaptureRenderState(node.AsWidget);
				ro.Material = material;
				ro.Mesh = mesh;
				ro.ChangeableResources = GetProtectedResources();
				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class TimelineRenderObject : WidgetRenderObject
			{
				public ChartsMaterial Material;
				public RectanglesMesh Mesh;
				public IRenderingResources ChangeableResources;

				public override void Render()
				{
					Renderer.MainRenderList.Flush();
					PrepareRenderState();
					Material.Matrix = Renderer.FixupWVP(
						ChangeableResources.RectanglesExtraTransform *
						(Matrix44)LocalToWorldTransform *
						Renderer.ViewProjection);
					Material.Apply(0);
					Mesh.Chunks = ChangeableResources.Chunks;
					Mesh.Draw();
					foreach (var l in ChangeableResources.Labels) {
						Renderer.DrawTextLine(l.Position, l.Text, 16, Color4.White, letterSpacing: 0);
					} 
				}
			}
		}

		internal struct TimelineState
		{
			public IEnumerable<TimePeriod> TimePeriods;
			public SpaceManager.Parameters SpaceManagerParameters;
			public TimePeriod VisibleTimePeriod;
			public Vector2 ContainerVisiblePartOffset;
			public Vector2 ContainerVisiblePartSize;
			public Vector2 LocalMousePosition;
		}

		internal interface IRenderingResources
		{
			/// <summary>
			/// Extra transform for <see cref="RectanglesMesh"/>.
			/// Has no effect on positioning <see cref="Labels"/>
			/// </summary>
			Matrix44 RectanglesExtraTransform { get; }
			List<RectanglesMesh.Chunk> Chunks { get; }
			List<RectangleLabel> Labels { get; }
		}

		internal struct RectangleLabel
		{
			public Vector2 Position;
			public string Text;
		}

		internal class RectanglesMesh
		{
			private List<Mesh<Vector3>> meshes = new List<Mesh<Vector3>>();
			private List<Chunk> chunks = new List<Chunk>();

			public List<Chunk> Chunks
			{
				get { return chunks; }
				set {
					while (chunks.Count > meshes.Count) {
						meshes.Add(new Mesh<Vector3> {
							Indices = null,
							Vertices = null,
							Topology = PrimitiveTopology.TriangleList,
							AttributeLocations = ChartsMaterial.ShaderProgram.MeshAttribLocations,
						});
					}
				}
			}

			public struct Chunk
			{
				public const int MaxVerticesCount = (65536 / Rectangle.VertexCount) * Rectangle.VertexCount;
				public const int MaxIndicesCount = (65536 / Rectangle.VertexCount) * Rectangle.IndexCount;

				public Vector3[] Vertices;
				public ushort[] Indices;
				public int VisibleRectanglesCount;
				public MeshDirtyFlags MeshDirtyFlags;
			}

			public void Draw()
			{
				for (int i = 0; i < chunks.Count; i++) {
					var chunk = chunks[i];
					var mesh = meshes[i];
					mesh.DirtyFlags = chunk.MeshDirtyFlags;
					mesh.Vertices = chunk.Vertices;
					mesh.Indices = chunk.Indices;
					mesh.DrawIndexed(0, Rectangle.IndexCount * chunk.VisibleRectanglesCount);
				}
			}
		}

		protected struct Rectangle
		{
			public const int VertexCount = 4;
			public const int IndexCount = 6;

			public Vector2 Position { get; }
			public Vector2 Size { get; }

			/// <summary>
			/// Index of color in <see cref="Colors"/>.
			/// </summary>
			public int ColorIndex { get; }

			public void WriteVerticesTo(Vector3[] buffer, int offset)
			{
				buffer[offset + 0] = new Vector3(Position.X,          Position.Y,          ColorIndex);
				buffer[offset + 1] = new Vector3(Position.X,          Position.Y + Size.Y, ColorIndex);
				buffer[offset + 2] = new Vector3(Position.X + Size.X, Position.Y + Size.Y, ColorIndex);
				buffer[offset + 3] = new Vector3(Position.X + Size.X, Position.Y,          ColorIndex);
			}

			public static void WriteIndicesTo(ushort[] buffer, int offset, int startVertex)
			{
				buffer[offset + 0] = (ushort)(startVertex + 0);
				buffer[offset + 1] = (ushort)(startVertex + 1);
				buffer[offset + 2] = (ushort)(startVertex + 2);
				buffer[offset + 3] = (ushort)(startVertex + 0);
				buffer[offset + 4] = (ushort)(startVertex + 2);
				buffer[offset + 5] = (ushort)(startVertex + 3);
			}
		}
	}
}
