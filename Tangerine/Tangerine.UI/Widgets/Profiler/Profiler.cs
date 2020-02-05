using Lime;

namespace Tangerine.UI
{
	public class Profiler : Widget
	{
		public Profiler(Widget panel)
		{
			//Presenter = new WidgetFlatFillPresenter(new Color4(53, 53, 55)); // todo
			Anchors = Anchors.LeftRight;
			//AddNode(new GpuProfiler());
			panel.AddNode(this);
		}
	}
}
