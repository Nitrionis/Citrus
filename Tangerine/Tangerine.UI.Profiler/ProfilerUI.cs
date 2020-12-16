#if PROFILER

using Lime;
using Lime.Profiler;
using Lime.Profiler.Contexts;
using Lime.Profiler.Graphics;
using Tangerine.Core;

namespace Tangerine.UI
{
	internal class ProfilerUI : Widget
	{
		private readonly ThemedDropDownList profilingMode;
		private readonly Widget optionsPanel;

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
			(ThemedButton, Material) CreateButton(string icon) {
				var material = new Material();
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
			Widget CreatePair(string name, out ThemedCheckBox checkBox) {
				checkBox = new ThemedCheckBox();
				return new Widget {
					Layout = new HBoxLayout {Spacing = 4},
					Nodes = {
						checkBox,
						new ThemedSimpleText(name)
					}
				};
			}
			optionsPanel = new Widget {
				Presenter = new WidgetFlatFillPresenter(
					ColorTheme.Current.IsDark ?
					Theme.Colors.GrayBackground.Lighten(0.06f) : 
					Theme.Colors.GrayBackground.Darken(0.06f)
				),
				Layout = new HBoxLayout { Spacing = 16 },
				Padding = new Thickness(4, 4, 0, 4),
				Anchors = Anchors.LeftRight,
				MaxHeight = 128,
				Nodes = {
					new Widget {
						Layout = new VBoxLayout(),
						Nodes = {
							new ThemedSimpleText("Charts") {
								Padding = new Thickness(4)
							},
							new Widget {
								Padding = new Thickness(4),
								Layout = new VBoxLayout { Spacing = 4 },
								Nodes = {
									CreatePair("CPU Main Thread", out var mainTheadChartsCheckBox),
									CreatePair("CPU Render Thread", out var renderTheadChartsCheckBox),
									CreatePair("GPU (In developing)", out var gpuChartsCheckBox),
									CreatePair("Scene Geometry", out var sceneGeometryChartsCheckBox),
									CreatePair("Full Geometry", out var fullGeometryChartsCheckBox),
								}
							}
						}
					},
					new Widget {
						Layout = new VBoxLayout(),
						Nodes = {
							new ThemedSimpleText("Timeline") {
								Padding = new Thickness(4)
							},
							new Widget {
								Padding = new Thickness(4),
								Layout = new VBoxLayout { Spacing = 4 },
								Nodes = {
									CreatePair("CPU Main Thread", out var mainTheadTimelineCheckBox),
									CreatePair("CPU Render Thread", out var renderTheadTimelineCheckBox),
									CreatePair("GPU (In developing)", out var gpuTimelineCheckBox),
								}
							}
						}
					},
					new Widget {
						Layout = new VBoxLayout(),
						Nodes = {
							new ThemedSimpleText("Options") {
								Padding = new Thickness(4)
							},
							new Widget {
								Padding = new Thickness(4),
								Layout = new VBoxLayout { Spacing = 4 },
								Nodes = {
									CreatePair("GPU Profiling (In developing)", out var gpuProfilingCheckBox),
									CreatePair("Batch Break Reasons", out var batchBreakReasonsCheckBox),
									CreatePair("Overdraw Mode", out var overdrawModeCheckBox),
								}
							}
						}
					},
					Spacer.HFill()
				}
			};
			optionsButton.Clicked += () => optionsPanel.Visible = !optionsPanel.Visible;
			Nodes.Add(optionsPanel);
			var (chartsSearchButton, chartsSearchMaterial) = CreateButton("Profiler.Search");
			var (timelineSearchButton, timelineSearchMaterial) = CreateButton("Profiler.Search");
			chartsSearchButton.Enabled = false;
			timelineSearchButton.Enabled = false;
			chartsSearchMaterial.Color = defaultButtonColor;
			timelineSearchMaterial.Color = defaultButtonColor;
			Nodes.Add(new Widget {
				Presenter = new WidgetFlatFillPresenter(
					ColorTheme.Current.IsDark ?
						Theme.Colors.GrayBackground.Lighten(0.1f) : 
						Theme.Colors.GrayBackground.Darken(0.06f)
				),
				Layout = new VBoxLayout(),
				Padding = new Thickness(4, 4, 0, 4),
				Nodes = {
					new Widget {
						Layout = new HBoxLayout(),
						Padding = new Thickness(4, 4, 0, 0),
						Nodes = {
							new ThemedSimpleText("Charts") {
								Padding = new Thickness(0, 21, 2, 0),
							},
							chartsSearchButton,
							new ThemedEditBox()
						}
					},
					new Widget {
						Layout = new HBoxLayout(),
						Padding = new Thickness(4, 4, 0, 0),
						Nodes = {
							new ThemedSimpleText("Timelines") {
								Padding = new Thickness(0, 4, 2, 0),
							},
							timelineSearchButton,
							new ThemedEditBox()
						}
					}
				}
			});
			Updating += delta => {
				recordMaterial.Color = 
					ProfilerTerminal.ProfilerEnabled ? recordButtonColor : defaultButtonColor;
				playMaterial.Color =
					ProfilerTerminal.Context is NativeContext && (Document.Current?.PreviewAnimation ?? false) || 
					ProfilerTerminal.Context is RemoteTerminalContext context && context.IsRemoteDeviceConnected ? 
						playButtonColor : defaultButtonColor;
				pauseMaterial.Color =
					ProfilerTerminal.IsSceneUpdateFrozen ? pauseButtonColor : defaultButtonColor;
				batchBreakReasonsCheckBox.Checked = ProfilerTerminal.BatchBreakReasonsRequired;
				overdrawModeCheckBox.Checked = Overdraw.Enabled;
			};
			batchBreakReasonsCheckBox.Clicked += () => 
				ProfilerTerminal.BatchBreakReasonsRequired = !ProfilerTerminal.BatchBreakReasonsRequired;
			overdrawModeCheckBox.Clicked += () =>
				ProfilerTerminal.OverdrawEnabled = !ProfilerTerminal.OverdrawEnabled;
		}

		private class Material : IMaterial
		{
			private readonly ShaderParams[] shaderParamsArray;
			private readonly ShaderParams shaderParams;
			private readonly ShaderParamKey<Vector4> colorKey;

			public Vector4 Color = Vector4.One;
			public Blending Blending;

			public string Id { get; set; }
			public int PassCount => 1;

			public Material()
			{
				shaderParams = new ShaderParams();
				shaderParamsArray = new[] { Renderer.GlobalShaderParams, shaderParams };
				colorKey = shaderParams.GetParamKey<Vector4>("color");
			}

			public void Apply(int pass)
			{
				shaderParams.Set(colorKey, Color);
				PlatformRenderer.SetBlendState(Blending.GetBlendState());
				PlatformRenderer.SetShaderProgram(IconShaderProgram.Instance);
				PlatformRenderer.SetShaderParams(shaderParamsArray);
			}

			public void Invalidate() { }

			public class IconShaderProgram : ShaderProgram
			{
				private static IconShaderProgram instance;
				public static IconShaderProgram Instance => instance ?? (instance = new IconShaderProgram());

				private const string VertexShader = @"
				attribute vec4 inPos;
				attribute vec2 inTexCoords1;

				uniform mat4 matProjection;

				varying lowp vec2 texCoords1;

				void main()
				{
					gl_Position = matProjection * inPos;
					texCoords1 = inTexCoords1;
				}";

				private const string FragmentShader = @"
				uniform lowp sampler2D tex1;
				uniform lowp vec4 color;

				varying lowp vec2 texCoords1;

				void main()
				{
					lowp float t = texture2D(tex1, texCoords1).r;
					gl_FragColor = vec4(color.rgb, t * t * t);
				}";

				private IconShaderProgram() : base(CreateShaders(), ShaderPrograms.Attributes.GetLocations(), ShaderPrograms.GetSamplers()) { }

				private static Shader[] CreateShaders()
				{
					return new Shader[] {
						new VertexShader(VertexShader),
						new FragmentShader(FragmentShader)
					};
				}
			}
		}
	}
}

#endif // PROFILER
