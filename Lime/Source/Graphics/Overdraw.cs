using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Lime.Graphics.Platform;

namespace Lime
{
	public static class Overdraw
	{
		public static bool Enabled { get; private set; } = true;

		public static readonly BlendState DefaultBlendState;
		//public static readonly string Step = (1f / 256f).ToString();
		public static readonly string Step = (1f / 8f).ToString();

		private static readonly string DefaultBody = $"gl_FragColor = vec4({Step},0,0,1);";
		private static readonly Regex ShaderBodyFinderRegex =
			new Regex(@"(?:\w*void\s+main\s*\(\s*(?:void)?\s*\)\W*{)");

		static Overdraw()
		{
			DefaultBlendState = new BlendState {
				Enable = true,
				BlendFunc = BlendFunc.Add,
				SrcBlend = Blend.One,
				DstBlend = Blend.One
			};
		}

		public static string ReplaceShaderBody(string shader, string newBody)
		{
			var parts = ShaderBodyFinderRegex.Split(shader);
			if (parts.Length != 2) {
				throw new InvalidOperationException();
			}
			string RemoveEntryPointBody(string body)
			{
				int bracesCounter = 1;
				for (int i = 0; i < body.Length; i++) {
					if (body[i] == '{') ++bracesCounter;
					if (body[i] == '}') {
						if (--bracesCounter == 0) {
							return body.Substring(i);
						}
					}
				}
				throw new InvalidOperationException();
			}
			return parts[0] + "void main() { " + newBody + RemoveEntryPointBody(parts[1]);
		}

		public static ShaderProgram CreateDefaultShaderProgram(
			IEnumerable<Shader>                       shaders,
			IEnumerable<ShaderProgram.AttribLocation> attribLocations,
			IEnumerable<ShaderProgram.Sampler>        samplers)
		{
			var newShaders = shaders.Select((s) => (Shader)new ShaderReplacer(s.Stage, s.Source));
			return new ShaderProgram(newShaders, attribLocations, samplers, Info.Empty);
		}

		private class ShaderReplacer : Shader
		{
			public ShaderReplacer(ShaderStageMask stage, string source) :
				base(stage, ReplaceBody(stage, source)) { }

			private static string ReplaceBody(ShaderStageMask stage, string source) =>
				stage == ShaderStageMask.Fragment ? ReplaceShaderBody(source, DefaultBody) : source;
		}

		public class Info
		{
			public static readonly Info Empty = new Info(null);

			public bool UseDefaultBlending { get; }
			public ShaderProgram Program { get; }

			public Info(ShaderProgram program, bool useDefaultBlending = true)
			{
				Program = program;
				UseDefaultBlending = useDefaultBlending;
			}
		}

		public class Controller
		{
			protected void SetEnabled(bool value) => Enabled = value;
		}
	}
}
