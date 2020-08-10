using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ShaderStageMask = Lime.Graphics.Platform.ShaderStageMask;

namespace Lime.Profiler.Graphics
{
	public static class Overdraw
	{
		private static bool enabled = true;
		private static bool required = true;

		public static bool Enabled
		{
			get => enabled;
			set => required = value;
		}

		public static void FrameRenderStarted() => enabled = required;
	}

	public class OverdrawInfo
	{
		public static readonly OverdrawInfo Empty;
		public static readonly BlendState DefaultBlending;

		static OverdrawInfo()
		{
			Empty = new OverdrawInfo();
			DefaultBlending = new BlendState {
				Enable    = true,
				BlendFunc = BlendFunc.Add,
				SrcBlend  = Blend.One,
				DstBlend  = Blend.One
			};
		}

		public BlendState Blending { get; }
		public ShaderProgram Program { get; }

		private OverdrawInfo() { }

		public OverdrawInfo(ShaderProgram program, BlendState blending)
		{
			Program = program;
			Blending = blending;
		}
	}

	public static class OverdrawShaderProgram
	{
		public static readonly string Step = (1f / 256f).ToString();

		public static ShaderProgram CreateDefault(
			IEnumerable<Shader>                       shaders,
			IEnumerable<ShaderProgram.AttribLocation> attribLocations,
			IEnumerable<ShaderProgram.Sampler>        samplers)
		{
			var newShaders = shaders.Select((s) => (Shader)new ShaderReplacer(s.Stage, s.Source));
			return new ShaderProgram(newShaders, attribLocations, samplers, OverdrawInfo.Empty);
		}

		private class ShaderReplacer : Shader
		{
			private static readonly string ReplacementBody = $"gl_FragColor = vec4({Step},0,0,1);";

			public ShaderReplacer(ShaderStageMask stage, string source) :
				base(stage, Regex.Replace(source, @"gl_FragColor[\s\r\n]*=[^;]*;", ReplacementBody)) { }
		}
	}

	public static class OverdrawInterpreter
	{
		private static void Fill(this Vector4[] arr, int start, int end, Vector4 value)
		{
			for (int i = start; i < end; i++) {
				arr[i] = value;
			}
		}

		public class Material : IMaterial
		{
			private static Material instance;
			public static Material Instance => instance ?? (instance = new Material());

			private static readonly BlendState blending;

			static Material()
			{
				blending = new BlendState {
					Enable    = true,
					BlendFunc = BlendFunc.Add,
					SrcBlend  = Blend.One,
					DstBlend  = Blend.Zero
				};
			}

			public string Id { get; set; }
			public int PassCount => 1;
			public readonly Vector4[] Colors;

			private readonly ShaderParams[] shaderParamsArray;
			private readonly ShaderParams shaderParams;
			private readonly ShaderParamKey<Vector4> colorsKey;

			private Material()
			{
				shaderParams = new ShaderParams();
				shaderParamsArray = new[] { Renderer.GlobalShaderParams, shaderParams };
				colorsKey = shaderParams.GetParamKey<Vector4>("colors");
				Colors = new Vector4[256];
				Colors[0] = new Vector4(0, 0, 0, 1);
				Colors[1] = new Vector4(0, 0, 102f/256f, 1);
				Colors[2] = new Vector4(0, 76f/256f, 1f, 1);
				Colors[3] = new Vector4(0, 178f/256f, 102f/256f, 1);
				Colors[4] = new Vector4(0, 1f, 0, 1);
				Colors.Fill(5, 8, new Vector4(204f / 256f, 204f / 256f, 0, 1));
				Colors.Fill(8, 11, new Vector4(1f, 76f / 256f, 0, 1));
				Colors.Fill(11, 16, new Vector4(178f / 256f, 0, 0, 1));
				Colors.Fill(16, 23, new Vector4(127f / 256f, 0, 127f / 256f, 1));
				Colors.Fill(23, 28, new Vector4(178f / 256f, 76f / 256f, 178f / 256f, 1));
				Colors.Fill(28, 38, new Vector4(1f, 229f / 256f, 229f / 256f, 1));
				Colors.Fill(38, Colors.Length, new Vector4(1, 1, 1, 1));
			}

			public void Apply(int pass)
			{
				shaderParams.Set(colorsKey, Colors, Colors.Length);
				PlatformRenderer.SetBlendState(blending);
				PlatformRenderer.SetShaderProgram(ShaderProgram.Instance);
				PlatformRenderer.SetShaderParams(shaderParamsArray);
			}

			public void Invalidate() { }

			public class ShaderProgram : Lime.ShaderProgram
			{
				private static ShaderProgram instance;
				public static ShaderProgram Instance => instance ?? (instance = new ShaderProgram());

				private const string VertexShader = @"
					attribute vec4 inPos;
					attribute vec4 inColor;
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
					uniform lowp vec4 colors[256];

					varying lowp vec2 texCoords1;

					void main()
					{
						highp float value = texture2D(tex1, texCoords1).r;
						gl_FragColor = colors[int(floor(value * 256.0 + 0.5))];
					}";

				private ShaderProgram() : base(
					CreateShaders(),
					ShaderPrograms.Attributes.GetLocations(),
					ShaderPrograms.GetSamplers(),
					OverdrawInfo.Empty) { }

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
