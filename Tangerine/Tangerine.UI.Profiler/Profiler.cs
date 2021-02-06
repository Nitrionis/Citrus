using System;
using Lime;
using Tangerine.UI.Docking;

namespace Tangerine.UI
{
	public class Profiler
	{
		public static Profiler Instance { get; private set; }

		private readonly Panel panel;
		public readonly Widget RootWidget;

		public Profiler(Panel panel)
		{
			if (Instance != null) {
				throw new InvalidOperationException();
			}
			Instance = this;
			this.panel = panel;
			RootWidget = new Widget {
				Layout = new VBoxLayout {
					Spacing = 6
				}
			};
			panel.ContentWidget.AddNode(RootWidget);
#if PROFILER
			var sw = new ThemedScrollView(ScrollDirection.Horizontal);
			var bt1 = new ThemedButton("SW MinMaxSize 300") { MinMaxWidth = 500};
			bt1.Clicked += () => sw.MinMaxSize = new Vector2(300, 200);
			var bt2 = new ThemedButton("SW MinMaxSize 500") { MinMaxWidth = 500};
			bt2.Clicked += () => sw.MinMaxSize = new Vector2(500, 200);
			var bt3 = new ThemedButton("SW MinMaxSize 700") { MinMaxWidth = 500};
			bt3.Clicked += () => sw.MinMaxSize = new Vector2(700, 200);
			var bt4 = new ThemedButton("Content MinMaxSize 400") { MinMaxWidth = 500};
			bt4.Clicked += () => sw.Content.MinMaxSize = new Vector2(400, 100);
			var bt5 = new ThemedButton("Content MinMaxSize 500") { MinMaxWidth = 500};
			bt5.Clicked += () => sw.Content.MinMaxSize = new Vector2(500, 100);
			var bt6 = new ThemedButton("Content MinMaxSize 600") { MinMaxWidth = 500};
			bt6.Clicked += () => sw.Content.MinMaxSize = new Vector2(600, 100);
			var bt7 = new ThemedButton("Content MinMaxSize 800") { MinMaxWidth = 500};
			bt7.Clicked += () => sw.Content.MinMaxSize = new Vector2(800, 100);
			sw.MinMaxSize = new Vector2(500, 200);
			sw.Content.Presenter = new WidgetFlatFillPresenter(Color4.Gray);
			sw.Content.Layout = new HBoxLayout();
			sw.Content.AddNode(new Widget {
				Presenter = new WidgetFlatFillPresenter(Color4.Blue),
				MinMaxSize = new Vector2(500, 100),
				Layout = new HBoxLayout(),
				Nodes = {
					new Widget {
						Presenter = new WidgetFlatFillPresenter(Color4.Red),
						MinMaxSize = new Vector2(200, 100),
					},
					new Widget {
						Presenter = new WidgetFlatFillPresenter(Color4.Green),
						MinMaxSize = new Vector2(200, 100),
					}
				}
			});
			RootWidget.AddNode(bt1);
			RootWidget.AddNode(bt2);
			RootWidget.AddNode(bt3);
			RootWidget.AddNode(bt4);
			RootWidget.AddNode(bt5);
			RootWidget.AddNode(bt6);
			RootWidget.AddNode(bt7);
			RootWidget.AddNode(sw);
			/*var tabs = new ThemedTabbedWidget();
			tabs.AddTab("Overdraw", new OverdrawController(), isActive: true);
			RootWidget.AddNode(tabs);*/
#endif // PROFILER
		}
	}
}
