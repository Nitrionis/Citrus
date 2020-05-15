using System.Collections;
using System.Collections.Generic;
using Lime.Graphics.Platform.Profiling;

namespace Lime.SignedDistanceField
{
	internal class SDFRenderObject : TextRenderObject
	{
		public Sprite.IMaterialProvider MaterialProvider;
		public Vector2 Offset;

		public override void Render()
		{
			Renderer.Transform1 = Matrix32.Translation(Offset) * LocalToWorldTransform;
			SpriteList.Render(Color, MaterialProvider);
		}
	}

	public class SDFRenderObjectList : RenderObject
	{
		public List<TextRenderObject> Objects = new List<TextRenderObject>();

		public override void Render()
		{
			foreach (var ro in Objects) {
#if LIME_PROFILER
				ro.SetGlobalProfilerData();
				var usage = RenderCpuProfiler.NodeCpuUsageStarted(ro.Node, ro.Manager);
#endif
				ro.Render();
#if LIME_PROFILER
				RenderCpuProfiler.NodeCpuUsageFinished(usage);
				ro.ResetGlobalProfilerData();
#endif
			}
		}

		protected override void OnRelease()
		{
			foreach (var ro in Objects) {
				ro.Release();
			}
			Objects.Clear();
			base.OnRelease();
		}
	}
}
