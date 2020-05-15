using Lime;
using System.Runtime.InteropServices;

namespace Tangerine.UI.Timeline
{
	internal class TimelineMaterial : IMaterial
	{
		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
		public struct Vertex
		{
			public Vector4 Position;
			public Color4 Color;
		}

		private readonly ShaderParams[] shaderParamsArray;
		private readonly ShaderParamKey<Matrix44> matrixKey;

		public Matrix44 Matrix = Matrix44.Identity;

		public string Id { get; set; }
		public int PassCount => 1;

		public TimelineMaterial()
		{
			shaderParamsArray = new[] { new ShaderParams(), new ShaderParams() };
			matrixKey = shaderParamsArray[0].GetParamKey<Matrix44>("wvp");
		}

		public void Apply(int pass)
		{
			PlatformRenderer.SetShaderProgram(ShaderProgram.Instance);
			shaderParamsArray[0].Set(matrixKey, Matrix);
			PlatformRenderer.SetShaderParams(shaderParamsArray);
		}

		public IMaterial Clone() => new ChartMaterial();

		public void Invalidate() { }

		public class ShaderProgram : Lime.ShaderProgram
		{
			private static ShaderProgram instance;
			public static ShaderProgram Instance => instance ?? (instance = new ShaderProgram());

			public static readonly AttribLocation[] AttribLocations;
			public static readonly int[] MeshAttribLocations;

			private const string VertexShader = @"
				uniform mat4 wvp;
				attribute vec4 inPos;
				attribute vec4 inColor;
				varying lowp vec4 outColor;
				void main() {
					outColor = inColor;
					gl_Position = wvp * inPos;
				}";

			private const string FragmentShader = @"
				varying lowp vec4 outColor;
				void main() { gl_FragColor = outColor; }";

			static ShaderProgram()
			{
				AttribLocations = new[] {
					new AttribLocation { Index = 0, Name = "inPos" },
					new AttribLocation { Index = 1, Name = "inColor" }
				};
				MeshAttribLocations = new[] { 0, 1 };
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
