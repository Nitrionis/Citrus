using System.Collections;
using System.Collections.Generic;

namespace Lime
{
	public abstract class RenderObject
	{
#if PROFILER_GPU
		public static object CurrentNode { get; private set; }
		public static object CurrentManager { get; private set; }

		private object node;
		private object manager;
#endif

		internal bool Free = true;

		public abstract void Render();

		public void Release()
		{
			if (Free) return;
			try {
				OnRelease();
			} finally {
				Free = true;
			}
		}

		protected virtual void OnRelease() { }

#if PROFILER_GPU
		/// <summary>
		/// Must be called AFTER each <see cref="IPresenter.GetRenderObject(Node)"/>.
		/// </summary>
		public void SetProfilerData(object node, object manager)
		{
			this.node = node;
			this.manager = manager;
		}

		/// <summary>
		/// Must be called BEFORE each <see cref="Render"/>.
		/// </summary>
		public void SetGlobalProfilerData()
		{
			CurrentNode = node;
			CurrentManager = manager;
		}

		/// <summary>
		/// Must be called AFTER each <see cref="Render"/>.
		/// </summary>
		public void ResetGlobalProfilerData()
		{
			CurrentNode = null;
			CurrentManager = null;
		}
#endif
	}

	public class RenderObjectList : IReadOnlyList<RenderObject>, IEnumerable<RenderObject>
	{
		private List<RenderObject> objects = new List<RenderObject>();

		public int Count => objects.Count;

		public RenderObject this[int index] => objects[index];

		public void Add(RenderObject obj)
		{
			objects.Add(obj);
		}
		
		public void Clear()
		{
			foreach (var obj in objects) {
				obj.Release();
			}
			objects.Clear();
		}
		
		public void Render()
		{
			foreach (var ro in objects) {
#if PROFILER_GPU
				ro.SetGlobalProfilerData();
#endif
				ro.Render();
#if PROFILER_GPU
				ro.ResetGlobalProfilerData();
#endif
			}
		}

		public List<RenderObject>.Enumerator GetEnumerator() => objects.GetEnumerator();

		IEnumerator<RenderObject> IEnumerable<RenderObject>.GetEnumerator() => GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	public abstract class WidgetRenderObject : RenderObject
	{
		public Matrix32 LocalToWorldTransform;
		public Blending Blending;
		public ShaderId Shader;

		public void CaptureRenderState(Widget widget)
		{
			LocalToWorldTransform = widget.LocalToWorldTransform;
			Blending = widget.GlobalBlending;
			Shader = widget.GlobalShader;
		}

		protected void PrepareRenderState()
		{
			Renderer.Transform1 = LocalToWorldTransform;
			Renderer.Blending = Blending;
			Renderer.Shader = Shader;
		}
	}

	public static class RenderObjectPool<T> where T: RenderObject, new()
	{
		private static T[] items = new T[1] { new T() };
		private static int index;

		public static T Acquire()
		{
			for (int i = 0; i < items.Length; i++) {
				var item = items[index++];
				if (index == items.Length)
					index = 0;
				if (item.Free) {
					item.Free = false;
					return item;
				}
			}
			System.Array.Resize(ref items, items.Length * 2);
			index = items.Length / 2;
			for (int i = index; i < items.Length; i++) {
				items[i] = new T();
			}
			items[index].Free = false;
			return items[index];
		}
	}
}
