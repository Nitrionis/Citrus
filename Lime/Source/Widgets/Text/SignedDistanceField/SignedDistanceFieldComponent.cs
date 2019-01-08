using Yuzu;
using Lime.SignedDistanceField;

namespace Lime
{
	[TangerineRegisterComponent]
	[AllowedComponentOwnerTypes(typeof(SimpleText), typeof(RichText))]
	public class SignedDistanceFieldComponent : NodeComponent
	{
		private const string GroupFont = "01. Face";
		private const string GroupOutline = "02. Outline";
		private const string GroupUnderlay = "02. Underlay";
		private const string GroupGradient = "03. Gradient";
		private const string GroupBevel = "04. Bevel";
		private const float MinimumSoftness = 0f;
		private const float MaximumSoftness = 50f;
		private const float MinimumDilate = -30f;
		private const float MaximumDilate = 30f;
		private const float MinimumThickness = 0f;
		private const float MaximumThickness = 30f;
		private const float MinimumUnderlaySoftness = 0f;
		private const float MaximumUnderlaySoftness = 45f;
		private const float MinimumUnderlayDilate = -40f;
		private const float MaximumUnderlayDilate = 40f;
		private const float MinimumLightAngle = 0f;
		private const float MaximumLightAngle = 360f;
		private const float MinimumReflectionPower = 0f;
		private const float MaximumReflectionPower = 100f;
		private const float MinimumBevelRoundness = 0f;
		private const float MaximumBevelRoundness = 5f;
		private const float MinimumBevelWidth = 0f;
		private const float MaximumBevelWidth = 30f;

		internal SDFMaterialProvider SDFMaterialProvider { get; private set; } = new SDFMaterialProvider();
		internal SDFUnderlayMaterialProvider UnderlayMaterialProvider { get; private set; } = new SDFUnderlayMaterialProvider();

		private SDFPresenter presenter = new SDFPresenter();
		private SDFRenderChainBuilder renderChainBuilder = new SDFRenderChainBuilder();
		private float softness = 0f;
		private float dilate = 0f;
		private float thickness = 0f;
		private float underlaySoftness = 0f;
		private float underlayDilate = 0f;
		private Vector2 underlayOffset = new Vector2();
		private float lightAngle;
		private float reflectionPower;
		private float bevelRoundness;
		private float bevelWidth;

		[YuzuMember]
		[TangerineGroup(GroupFont)]
		public float Softness
		{
			get => softness;
			set => softness = Mathf.Clamp(value, MinimumSoftness, MaximumSoftness);
		}

		[YuzuMember]
		[TangerineGroup(GroupFont)]
		public float Dilate
		{
			get => dilate;
			set => dilate = Mathf.Clamp(value, MinimumDilate, MaximumDilate);
		}

		[YuzuMember]
		[TangerineGroup(GroupOutline)]
		public Color4 OutlineColor { get; set; } = Color4.Black;

		[YuzuMember]
		[TangerineGroup(GroupOutline)]
		public float Thickness
		{
			get => thickness;
			set => thickness = Mathf.Clamp(value, MinimumThickness, MaximumThickness);
		}

		[YuzuMember]
		[TangerineGroup(GroupUnderlay)]
		public bool UnderlayEnabled { get; set; }

		[YuzuMember]
		[TangerineGroup(GroupUnderlay)]
		public Color4 UnderlayColor { get; set; } = Color4.Black;

		[YuzuMember]
		[TangerineGroup(GroupUnderlay)]
		public Vector2 UnderlayOffset
		{
			get => underlayOffset;
			set => underlayOffset = value;
		}

		[YuzuMember]
		[TangerineGroup(GroupUnderlay)]
		public float UnderlaySoftness
		{
			get => underlaySoftness;
			set => underlaySoftness = Mathf.Clamp(value, MinimumUnderlaySoftness, MaximumUnderlaySoftness);
		}

		[YuzuMember]
		[TangerineGroup(GroupUnderlay)]
		public float UnderlayDilate
		{
			get => underlayDilate;
			set => underlayDilate = Mathf.Clamp(value, MinimumUnderlayDilate, MaximumUnderlayDilate);
		}

		[YuzuMember]
		[TangerineGroup(GroupGradient)]
		public bool GradientEnabled { get; set; }

		[YuzuMember]
		[TangerineGroup(GroupGradient)]
		public ColorGradient Gradient { get; set; } = new ColorGradient(Color4.White, Color4.Black);

		[YuzuMember]
		[TangerineGroup(GroupGradient)]
		public float GradientAngle { get; set; }

		[YuzuMember]
		[TangerineGroup(GroupBevel)]
		public bool BevelEnabled { get; set; }

		[YuzuMember]
		[TangerineGroup(GroupBevel)]
		public Color4 LightColor { get; set; } = Color4.White;

		[YuzuMember]
		[TangerineGroup(GroupBevel)]
		public float LightAngle
		{
			get => lightAngle;
			set => lightAngle = Mathf.Clamp(value, MinimumLightAngle, MaximumLightAngle);
		}

		[YuzuMember]
		[TangerineGroup(GroupBevel)]
		public float ReflectionPower
		{
			get => reflectionPower;
			set => reflectionPower = Mathf.Clamp(value, MinimumReflectionPower, MaximumReflectionPower);
		}

		[YuzuMember]
		[TangerineGroup(GroupBevel)]
		public float BevelRoundness
		{
			get => bevelRoundness;
			set => bevelRoundness = Mathf.Clamp(value, MinimumBevelRoundness, MaximumBevelRoundness);
		}

		[YuzuMember]
		[TangerineGroup(GroupBevel)]
		public float BevelWidth
		{
			get => bevelWidth;
			set => bevelWidth = Mathf.Clamp(value, MinimumBevelWidth, MaximumBevelWidth);
		}

		public void GetOwnerRenderObjects(RenderChain renderChain, RenderObjectList roObjects)
		{
			DettachFromNode(Owner);
			Owner.AddToRenderChain(renderChain);
			renderChain.GetRenderObjects(roObjects);
			AttachToNode(Owner);
		}

		protected override void OnOwnerChanged(Node oldOwner)
		{
			base.OnOwnerChanged(oldOwner);
			if (oldOwner != null) {
				DettachFromNode(oldOwner);
			}
			if (Owner != null) {
				AttachToNode(Owner);
			}
		}

		private void AttachToNode(Node node)
		{
			node.Presenter = presenter;
			node.RenderChainBuilder = renderChainBuilder;
			renderChainBuilder.Owner = node.AsWidget;
		}

		private void DettachFromNode(Node node)
		{
			node.RenderChainBuilder = node;
			node.Presenter = DefaultPresenter.Instance;
			renderChainBuilder.Owner = null;
		}

		public override NodeComponent Clone()
		{
			var clone = (SignedDistanceFieldComponent)base.Clone();
			clone.presenter = (SDFPresenter)presenter.Clone();
			clone.renderChainBuilder = (SDFRenderChainBuilder)renderChainBuilder.Clone(null);
			clone.SDFMaterialProvider = (SDFMaterialProvider)SDFMaterialProvider.Clone();
			return clone;
		}
	}
}
