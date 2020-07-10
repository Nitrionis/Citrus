using Lime;

namespace Tangerine.UI
{
	internal class ChartMaterial : IMaterial
	{
		private readonly ShaderParams[] shaderParamsArray;
		private readonly ShaderParamKey<Matrix44> matrixKey;
		private readonly ShaderParamKey<float> startPosKey;
		private readonly ShaderParamKey<float> burnRangeKey;
		private readonly ShaderParamKey<Vector4> burntColorKey;
		private readonly ShaderParamKey<Vector4> colorsKeys;

		public Matrix44 Matrix = Matrix44.Identity;
		public float NewestItemPosition;
		public float BurnRange;
		public Vector4 BurntColor;
		public Vector4[] Colors;

		public string Id { get; set; }
		public int PassCount => 1;

		public ChartMaterial()
		{
			shaderParamsArray = new[] { new ShaderParams(), new ShaderParams() };
			matrixKey      = shaderParamsArray[0].GetParamKey<Matrix44>("wvp");
			startPosKey    = shaderParamsArray[0].GetParamKey<float>("newestItemPosition");
			burnRangeKey   = shaderParamsArray[0].GetParamKey<float>("burnRange");
			burntColorKey  = shaderParamsArray[0].GetParamKey<Vector4>("burntColor");
			colorsKeys     = shaderParamsArray[0].GetParamKey<Vector4>("colors");
		}

		public void Apply(int pass)
		{
			PlatformRenderer.SetShaderProgram(ShaderProgram.Instance);
			shaderParamsArray[0].Set(matrixKey, Matrix);
			shaderParamsArray[0].Set(startPosKey, NewestItemPosition);
			shaderParamsArray[0].Set(burnRangeKey, BurnRange);
			shaderParamsArray[0].Set(burntColorKey, BurntColor);
			shaderParamsArray[0].Set(colorsKeys, Colors, Colors.Length);
			PlatformRenderer.SetShaderParams(shaderParamsArray);
		}

		public void Invalidate() { }

		public class ShaderProgram : Lime.ShaderProgram
		{
			private static ShaderProgram instance;
			public static ShaderProgram Instance => instance ?? (instance = new ShaderProgram());

			public static readonly AttribLocation[] AttribLocations;
			public static readonly int[] MeshAttribLocations;

			private const string VertexShader = @"
				uniform mat4 wvp;
				uniform float newestItemPosition;
				uniform float burnRange;
				uniform vec3 burntColor;
				uniform lowp vec4 colors[13];

				attribute vec3 inPos;
				varying lowp vec3 color;

				void main() {
					float mixFactor = (newestItemPosition - inPos.x) / burnRange;
					float sign_0_1 = sign(mixFactor) * 0.5 + 0.5;
					mixFactor = sign_0_1 * mixFactor + (1.0 - sign_0_1) * (1.0 - mixFactor);
					vec3 originalColor = colors[int(inPos.z)];
					color = mix(originalColor, burntColor, mixFactor);
					gl_Position = wvp * vec4(inPos.xy, 0.0, 1.0);
				}";

			private const string FragmentShader = @"
				varying lowp vec3 color;
				void main() { gl_FragColor = color; }";

			static ShaderProgram()
			{
				AttribLocations = new[] { new AttribLocation() { Index = 0, Name = "inPos" } };
				MeshAttribLocations = new[] { 0 };
			}

			private ShaderProgram() : base(CreateShaders(), AttribLocations, new Sampler[0]) { }

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
