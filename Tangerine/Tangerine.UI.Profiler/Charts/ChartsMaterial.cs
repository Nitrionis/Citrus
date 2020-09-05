using Lime;

namespace Tangerine.UI.Charts
{
	internal class ChartsMaterial : IMaterial
	{
		public const int ColorsCount = 16;

		private readonly ShaderParams[] shaderParamsArray;
		private readonly ShaderParamKey<Matrix44> matrixKey;
		private readonly ShaderParamKey<Vector4> colorsKeys;

		public Matrix44 Matrix = Matrix44.Identity;
		public Vector4[] Colors { get; private set; } = new Vector4[ColorsCount];

		public string Id { get; set; }
		public int PassCount => 1;

		public ChartsMaterial()
		{
			shaderParamsArray = new[] { new ShaderParams() };
			matrixKey = shaderParamsArray[0].GetParamKey<Matrix44>("wvp");
			colorsKeys = shaderParamsArray[0].GetParamKey<Vector4>("colors");
		}

		public void Apply(int pass)
		{
			PlatformRenderer.SetShaderProgram(ShaderProgram.Instance);
			shaderParamsArray[0].Set(matrixKey, Matrix);
			shaderParamsArray[0].Set(colorsKeys, Colors, Colors.Length);
			PlatformRenderer.SetShaderParams(shaderParamsArray);
		}

		public IMaterial Clone() => new ChartsMaterial() {
			Colors = (Vector4[])Colors.Clone()
		};

		public void Invalidate() { }

		public class ShaderProgram : Lime.ShaderProgram
		{
			private static ShaderProgram instance;
			public static ShaderProgram Instance => instance ?? (instance = new ShaderProgram());

			public static readonly AttribLocation[] AttribLocations;
			public static readonly int[] MeshAttribLocations;

			private const string VertexShader = @"
				uniform mat4 wvp;
				uniform lowp vec4 colors[16];
				attribute vec3 vertex; // z is color index
				varying highp vec4 color;
				void main() {
					color = colors[int(vertex.z)];
					gl_Position = wvp * vec4(vertex.xy, 0, 1);
				}";

			private const string FragmentShader = @"
				varying highp vec4 color;
				void main() { gl_FragColor = color; }";

			static ShaderProgram()
			{
				AttribLocations = new[] { new AttribLocation() { Index = 0, Name = "vertex" } };
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
