#if PROFILER

using Lime;

namespace Tangerine.UI
{
	internal class IconMaterial : IMaterial
	{
		private readonly ShaderParams[] shaderParamsArray;
		private readonly ShaderParams shaderParams;
		private readonly ShaderParamKey<Vector4> colorKey;

		public Vector4 Color = Vector4.One;
		public Blending Blending;

		public string Id { get; set; }
		public int PassCount => 1;

		public IconMaterial()
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

#endif // PROFILER