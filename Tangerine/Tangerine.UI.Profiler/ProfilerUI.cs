#if PROFILER

using System;
using Lime;
using Lime.Profiler;
using Lime.Profiler.Contexts;
using Lime.Profiler.Graphics;
using Tangerine.Core;
using Tangerine.UI.Charts;

namespace Tangerine.UI
{
	internal class ProfilerUI : Widget
	{
		private readonly ThemedDropDownList profilingMode;

		private readonly Vector4 defaultButtonColor = Color4.Black.Lighten(0.2f).ToVector4();
		private readonly Vector4 recordButtonColor = new Color4(255, 87, 34).ToVector4();
		private readonly Vector4 playButtonColor = new Color4(33, 150, 243).ToVector4();
		private readonly Vector4 pauseButtonColor = new Color4(255, 152, 0).ToVector4();
		
		public ProfilerUI()
		{
			Layout = new VBoxLayout();
			Anchors = Anchors.LeftRight;
			var size = new Vector2(28, 22);
			var padding = new Thickness(5, 2);
			(ThemedButton, IconMaterial) CreateButton(string icon) {
				var material = new IconMaterial();
				return (new ThemedButton {
					MinMaxSize = size,
					LayoutCell = new LayoutCell(Alignment.Center),
					Nodes = {
						new Image(IconPool.GetTexture(icon)) {
							MinMaxSize = size,
							Size = size,
							Padding = padding,
							Material = material,
						}
					}
				}, material);
			}
			var (recordButton, recordMaterial) = CreateButton("Profiler.Radio");
			recordButton.Clicked += () => {
				ProfilerTerminal.ProfilerEnabled = !ProfilerTerminal.ProfilerEnabled;
			};
			var (playButton, playMaterial) = CreateButton("Profiler.Play");
			playButton.Clicked += () => {
				if (ProfilerTerminal.Context is NativeContext) {
					Document.Current?.TogglePreviewAnimation();
				}
			};
			var (pauseButton, pauseMaterial) = CreateButton("Profiler.Pause");
			pauseButton.Clicked += () => {
				var nextState = ProfilerTerminal.IsSceneUpdateFrozen ?
					UpdateSkipOptions.NoSkip : UpdateSkipOptions.SkipAll;
				ProfilerTerminal.SetSceneUpdateFrozen(nextState);
			};
			var (playNextButton, playNextMaterial) = CreateButton("Profiler.SkipNext");
			playNextButton.Clicked += () => {
				ProfilerTerminal.SetSceneUpdateFrozen(UpdateSkipOptions.SkipAllAfterNext);
			};
			playNextMaterial.Color = defaultButtonColor;
			profilingMode = new ThemedDropDownList { MinMaxWidth = 104 };
			profilingMode.Items.Add(new CommonDropDownList.Item("Tangerine"));
			profilingMode.Items.Add(new CommonDropDownList.Item("Remote Game"));
			profilingMode.Index = 0;
			var optionsButton = new ThemedButton("Options");
			Nodes.Add(new Widget {
				Presenter = new WidgetFlatFillPresenter(Theme.Colors.ControlBorder),
				Layout = new HBoxLayout(),
				Padding = new Thickness(4),
				Anchors = Anchors.LeftRight,
				MaxWidth = float.PositiveInfinity,
				MaxHeight = size.Y,
				Nodes = {
					new ThemedSimpleText("Profiler") {
						VAlignment = VAlignment.Center,
						Padding = new Thickness(4, 4, 2, 0)
					},
					recordButton,
					new ThemedSimpleText("Scene") {
						VAlignment = VAlignment.Center,
						Padding = new Thickness(8, 4, 2, 0)
					},
					playButton,
					pauseButton,
					playNextButton,
					new ThemedSimpleText("Data source") {
						VAlignment = VAlignment.Center,
						Padding = new Thickness(8, 4, 2, 0)
					},
					profilingMode,
					optionsButton,
					Spacer.HFill()
				}
			});
			Image CreateSearchIcon() => new Image(IconPool.GetTexture("Profiler.Search")) {
				Material = new IconMaterial {
					Color = defaultButtonColor
				},
				Padding = new Thickness(2),
				MinMaxSize = new Vector2(22, 22),
				Size = new Vector2(22, 22),
			};
			var chartsPanel = new ChartsPanel(out var chartVisibilityControllers);
			var optionsPanel = new OptionsPanel(chartVisibilityControllers, new TimelineVisibilityControllers {
				MainThreadTimelineSetVisible = (b => { }),
				RenderThreadTimelineSetVisible = (b => {}),
				GpuTimelineSetVisible = (b => {})
			});
			optionsButton.Clicked += () => optionsPanel.Visible = !optionsPanel.Visible;
			Nodes.Add(optionsPanel);
			Nodes.Add(chartsPanel);
			Updating += delta => {
				recordMaterial.Color = 
					ProfilerTerminal.ProfilerEnabled ? recordButtonColor : defaultButtonColor;
				playMaterial.Color =
					ProfilerTerminal.Context is NativeContext && (Document.Current?.PreviewAnimation ?? false) || 
					ProfilerTerminal.Context is RemoteTerminalContext context && context.IsRemoteDeviceConnected ? 
						playButtonColor : defaultButtonColor;
				pauseMaterial.Color =
					ProfilerTerminal.IsSceneUpdateFrozen ? pauseButtonColor : defaultButtonColor;
			};
		}
	}
}

#endif // PROFILER
