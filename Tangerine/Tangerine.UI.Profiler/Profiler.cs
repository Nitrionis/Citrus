using System;
using System.Collections.Generic;
using Lime;
using Tangerine.UI.Docking;

namespace Tangerine.UI
{
	public class Profiler
	{
		public static Profiler Instance { get; private set; }

		private readonly Panel panel;
		public readonly Widget RootWidget;

		private float scale = 1;
		private ThemedScrollView sv;
		private Widget content;
		
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
			sv = new ThemedScrollView(ScrollDirection.Horizontal);
			var bt1 = new ThemedButton("SW MinMaxSize 300") { MinMaxWidth = 500};
			bt1.Clicked += () => sv.MinMaxSize = new Vector2(300, 200);
			var bt2 = new ThemedButton("SW MinMaxSize 500") { MinMaxWidth = 500};
			bt2.Clicked += () => sv.MinMaxSize = new Vector2(500, 200);
			var bt3 = new ThemedButton("SW MinMaxSize 700") { MinMaxWidth = 500};
			bt3.Clicked += () => sv.MinMaxSize = new Vector2(700, 200);
			var bt4 = new ThemedButton("Content MinMaxSize 400") { MinMaxWidth = 500};
			bt4.Clicked += () => sv.Content.MinMaxSize = new Vector2(400, 100);
			var bt5 = new ThemedButton("Content MinMaxSize 500") { MinMaxWidth = 500};
			bt5.Clicked += () => sv.Content.MinMaxSize = new Vector2(500, 100);
			var bt6 = new ThemedButton("Content MinMaxSize 600") { MinMaxWidth = 500};
			bt6.Clicked += () => sv.Content.MinMaxSize = new Vector2(600, 100);
			var bt7 = new ThemedButton("Content MinMaxSize 800") { MinMaxWidth = 500};
			bt7.Clicked += () => sv.Content.MinMaxSize = new Vector2(800, 100);
			sv.MinMaxSize = new Vector2(500, 200);
			sv.Content.Presenter = new WidgetFlatFillPresenter(Color4.Black);
			sv.Content.Layout = new HBoxLayout();
			
			sv.Content.AddNode(new Widget {
				Layout = new HBoxLayout(),
				Nodes = {
					new Widget {
						Presenter = new WidgetFlatFillPresenter(Color4.Gray),
						MinMaxSize = new Vector2(600, 100),
					},
					new Widget {
						Presenter = new WidgetFlatFillPresenter(Color4.Red),
						MinMaxSize = new Vector2(200, 100),
					},
					new Widget {
						Presenter = new WidgetFlatFillPresenter(Color4.Green),
						MinMaxSize = new Vector2(200, 100),
					},
					new Widget {
						Presenter = new WidgetFlatFillPresenter(Color4.Blue),
						MinMaxSize = new Vector2(200, 100),
					},
					new Widget {
						Presenter = new WidgetFlatFillPresenter(Color4.Gray),
						MinMaxSize = new Vector2(600, 100),
					},
				}
			});
			RootWidget.AddNode(bt1);
			RootWidget.AddNode(bt2);
			RootWidget.AddNode(bt3);
			RootWidget.AddNode(bt4);
			RootWidget.AddNode(bt5);
			RootWidget.AddNode(bt6);
			RootWidget.AddNode(bt7);
			RootWidget.AddNode(sv);
			sv.Content.Tasks.Insert(0, new Task(ScaleScroll()));
			sv.Content.Tasks.Insert(0, new Task(HorizontalScrollTask()));
			sv.Input.IsKeyPressed(Key.Control);
#endif // PROFILER
		}

		private IEnumerator<object> ScaleScroll()
		{
			while (true) {
				if (
					sv.Input.IsKeyPressed(Key.Control) &&
					(sv.Input.WasKeyPressed(Key.MouseWheelDown) || sv.Input.WasKeyPressed(Key.MouseWheelUp))
					)
				{
					scale += sv.Input.WheelScrollAmount / 1200f;
					scale = Mathf.Clamp(scale, 5f / 6f, 10f);
					int index = 0;
					foreach (var n in sv.Content.Nodes[0].Nodes) {
						if (index == 0 || index == sv.Content.Nodes[0].Nodes.Count - 1) {
							n.AsWidget.MinMaxWidth = 600 * scale;
						} else {
							n.AsWidget.MinMaxWidth = 200 * scale;
						}
						index++;
					}
					float sp = sv.ScrollPosition;
					float mp = sv.Content.LocalMousePosition().X;
					float oldWidth = sv.Content.Width;
					float newWidth = 1800 * scale;
					sv.Content.MinMaxWidth = newWidth;
					sv.ScrollPosition = (mp / oldWidth - (mp - sp) / newWidth) * newWidth;
				}
				yield return null;
			}
		}
		
		private IEnumerator<object> HorizontalScrollTask()
		{
			while (true) {
				bool isHorizontalMode = 
					!sv.Input.IsKeyPressed(Key.Shift) && 
					!sv.Input.IsKeyPressed(Key.Control);
				sv.Behaviour.CanScroll = isHorizontalMode;
				sv.Behaviour.StopScrolling();
				yield return null;
			}
		}
	}
}
