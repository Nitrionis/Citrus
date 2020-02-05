using System;
using Lime;
using Lime.Widgets.Charts;
using Lime.Graphics.Platform;

namespace Tangerine.UI
{
	public class Profiler : Widget
	{
		private AreaCharts areaCharts;

		public Profiler(Widget panel)
		{
			Anchors = Anchors.LeftRight;
			Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Profiler.Background);
			//AddNode(new GpuProfiler());
			panel.AddNode(this);

			var colors = new Color4[] {
				ColorTheme.Current.Profiler.ChartOne,
				ColorTheme.Current.Profiler.ChartTwo,
				ColorTheme.Current.Profiler.ChartThree,
				ColorTheme.Current.Profiler.ChartFour,
				ColorTheme.Current.Profiler.ChartFive,
				ColorTheme.Current.Profiler.ChartSix,
				ColorTheme.Current.Profiler.ChartSeven,
			};
			var parameters = new AreaCharts.Parameters(160, colors) {
				ChartsCount = 3, ChartHeight = 160
			};
			areaCharts = new AreaCharts(parameters) {
				BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground
			};
			AddNode(areaCharts);

			PlatformProfiler.Instance.OnFrameRenderCompleted = OnFrameRenderCompleted;
		}

		public void OnFrameRenderCompleted()
		{
			var random = new Random();
			var slice = new float[] {
				16f * (float)random.NextDouble(),
				16f * (float)random.NextDouble(),
				16f * (float)random.NextDouble(),
			};
			areaCharts.PushSlice(slice);
		}
	}
}
