using System.Collections.Generic;
using System.Text;

namespace Lime
{
	public class SDFOutlineMaterial : IMaterial
	{
		private static readonly BlendState disabledBlendingState = new BlendState { Enable = false };

		private readonly Blending blending;
		private readonly ShaderParams[] shaderParamsArray;
		private readonly ShaderParams shaderParams;
		private readonly ShaderParamKey<float> thicknessKey;
		private readonly ShaderParamKey<float> softnessKey;
		private readonly ShaderParamKey<Vector4> colorKey;

		public float Thickness { get; set; } = 0f;
		public float Softness { get; set; } = 0f;
		public Color4 Color { get; set; } = Color4.Black;

		public int PassCount => 1;

		public SDFOutlineMaterial() : this(Blending.Alpha) { }

		public SDFOutlineMaterial(Blending blending)
		{
			this.blending = blending;
			shaderParams = new ShaderParams();
			shaderParamsArray = new[] { Renderer.GlobalShaderParams, shaderParams };
			thicknessKey = shaderParams.GetParamKey<float>("thickness");
			softnessKey = shaderParams.GetParamKey<float>("softness");
			colorKey = shaderParams.GetParamKey<Vector4>("color");
		}

		public void Apply(int pass)
		{
			shaderParams.Set(thicknessKey, 0.5f - Thickness);
			shaderParams.Set(softnessKey, Softness);
			shaderParams.Set(colorKey, Color.ToVector4());
			PlatformRenderer.SetBlendState(blending.GetBlendState());
			PlatformRenderer.SetShaderProgram(SDFOutlineShaderProgram.GetInstance());
			PlatformRenderer.SetShaderParams(shaderParamsArray);
		}

		public void Invalidate() { }

		public IMaterial Clone()
		{
			return new SDFOutlineMaterial(blending) {
				Thickness = Thickness,
				Softness = Softness,
				Color = Color,
			};
		}
	}

	public class SDFOutlineMaterialProvider : Sprite.IMaterialProvider
	{
		public SDFOutlineMaterial Material = new SDFOutlineMaterial();
		public IMaterial GetMaterial(int tag) => Material;

		public Sprite.IMaterialProvider Clone() => new SDFOutlineMaterialProvider() {
			Material = Material
		};
	}

	public class SDFOutlineShaderProgram : ShaderProgram
	{
		private const string VertexShader = @"
			attribute vec4 inPos;
			attribute vec4 inColor;
			attribute vec2 inTexCoords1;

			uniform mat4 matProjection;

			varying lowp vec2 texCoords1;
			varying lowp vec4 v_color;

			void main()
			{
				gl_Position = matProjection * inPos;
				v_color = inColor;
				texCoords1 = inTexCoords1;
			}";

		private const string FragmentShader = @"
			varying lowp vec4 v_color;
			varying lowp vec2 texCoords1;
			uniform lowp sampler2D tex1;

			uniform lowp float thickness;
			uniform lowp float softness;
			uniform lowp vec4 color;

			void main() {
				lowp float distance = texture2D(tex1, texCoords1).r;
				lowp float outlineFactor = smoothstep(0.5 - softness, 0.5 + softness, distance);
				lowp vec4 c = mix(color, v_color, outlineFactor);
				lowp float alpha = smoothstep(thickness - softness, thickness + softness, distance);
				gl_FragColor = vec4(c.rgb, c.a * alpha);
			}";

		private static SDFOutlineShaderProgram instance;

		public static SDFOutlineShaderProgram GetInstance() => instance ?? (instance = new SDFOutlineShaderProgram());

		private SDFOutlineShaderProgram() : base(CreateShaders(), ShaderPrograms.Attributes.GetLocations(), ShaderPrograms.GetSamplers()) { }

		private static Shader[] CreateShaders()
		{
			return new Shader[] {
				new VertexShader(VertexShader),
				new FragmentShader(FragmentShader)
			};
		}
	}
}
