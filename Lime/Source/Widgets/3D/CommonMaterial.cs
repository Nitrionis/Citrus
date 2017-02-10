﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yuzu;

namespace Lime
{
	public class CommonMaterial : IMaterial, IMaterialSkin, IMaterialFog
	{
		private static Dictionary<CommonMaterialProgramSpec, CommonMaterialProgram> programCache =
			new Dictionary<CommonMaterialProgramSpec, CommonMaterialProgram>();

		private CommonMaterialProgram program;
		private ITexture diffuseTexture;
		private Matrix44[] boneTransforms;
		private int boneCount;
		private bool skinEnabled;
		private FogMode fogMode;

		[YuzuMember]
		public string Name { get; set; }

		[YuzuMember]
		public Color4 DiffuseColor { get; set; }

		[YuzuMember]
		public Color4 ColorFactor { get; set; }

		[YuzuMember]
		public Color4 FogColor { get; set; }

		[YuzuMember]
		public float FogStart { get; set; }

		[YuzuMember]
		public float FogEnd { get; set; }

		[YuzuMember]
		public float FogDensity { get; set; }

		[YuzuMember]
		public Blending Blending { get; set; }

		[YuzuMember]
		public ITexture DiffuseTexture
		{
			get { return diffuseTexture; }
			set
			{
				if (diffuseTexture != value) {
					diffuseTexture = value;
					program = null;
				}
			}
		}

		[YuzuMember]
		public bool SkinEnabled
		{
			get { return skinEnabled; }
			set
			{
				if (skinEnabled != value) {
					skinEnabled = value;
					program = null;
				}
			}
		}

		[YuzuMember]
		public FogMode FogMode
		{
			get { return fogMode; }
			set
			{
				if (fogMode != value) {
					fogMode = value;
					program = null;
				}
			}
		}

		public CommonMaterial()
		{
			DiffuseColor = Color4.White;
			ColorFactor = Color4.White;
			FogColor = Color4.White;
			Blending = Blending.Alpha;
		}

		public void SetBones(Matrix44[] boneTransforms, int boneCount)
		{
			this.boneTransforms = boneTransforms;
			this.boneCount = boneCount;
		}

		public void Apply()
		{
			PlatformRenderer.SetBlending(Blending);
			PrepareShaderProgram();
			program.Use();
			program.LoadMatrix(program.WorldViewProjUniformId, Renderer.FixupWVP(Renderer.WorldViewProjection));
			program.LoadColor(program.DiffuseColorUniformId, DiffuseColor * ColorFactor);
			if (skinEnabled) {
				program.LoadMatrixArray(program.BonesUniformId, boneTransforms, boneCount);
			}
			if (fogMode != FogMode.None) {
				program.LoadMatrix(program.WorldViewUniformId, Renderer.WorldView);
				program.LoadColor(program.FogColorUniformId, FogColor);
				if (fogMode == FogMode.Linear) {
					program.LoadFloat(program.FogStartUniformId, FogStart);
					program.LoadFloat(program.FogEndUniformId, FogEnd);
				} else {
					program.LoadFloat(program.FogDensityUniformId, FogDensity);
				}
			}
			if (diffuseTexture != null) {
				PlatformRenderer.SetTexture(diffuseTexture, CommonMaterialProgram.DiffuseTextureStage);
			}
		}

		private void PrepareShaderProgram()
		{
			if (program != null) {
				return;
			}
			var spec = new CommonMaterialProgramSpec {
				SkinEnabled = skinEnabled,
				DiffuseTextureEnabled = diffuseTexture != null,
				FogMode = FogMode
			};
			if (programCache.TryGetValue(spec, out program)) {
				return;
			}
			program = new CommonMaterialProgram(spec);
			programCache[spec] = program;
		}

		public IMaterial Clone()
		{
			return new CommonMaterial {
				Name = Name,
				DiffuseColor = DiffuseColor,
				ColorFactor = ColorFactor,
				FogMode = FogMode,
				FogColor = FogColor,
				FogStart = FogStart,
				FogEnd = FogEnd,
				FogDensity = FogDensity,
				Blending = Blending,
				DiffuseTexture = DiffuseTexture,
				SkinEnabled = SkinEnabled
			};
		}
	}
}
