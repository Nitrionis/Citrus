using Yuzu;

namespace Lime
{
	[YuzuCompact]
	public struct BoneWeight
	{
		[YuzuMember("0")]
		public int Index;

		[YuzuMember("1")]
		public float Weight;
	}

	[YuzuCompact]
	public class SkinningWeights
	{
		[YuzuMember("0")]
		public BoneWeight Bone0;

		[YuzuMember("1")]
		public BoneWeight Bone1;

		[YuzuMember("2")]
		public BoneWeight Bone2;

		[YuzuMember("3")]
		public BoneWeight Bone3;
	}

	public class Bone : Node
	{
		[YuzuMember]
		public Vector2 Position { get; set; }

		[YuzuMember]
		public float Rotation { get; set; }

		[YuzuMember]
		public float Length { get; set; }

		[YuzuMember]
		public bool IKStopper { get; set; }

		[YuzuMember]
		public int Index { get; set; }

		[YuzuMember]
		public int BaseIndex { get; set; }

		[YuzuMember]
		public float EffectiveRadius { get; set; }

		[YuzuMember]
		public float FadeoutZone { get; set; }

		[YuzuMember]
		public Vector2 RefPosition { get; set; }

		[YuzuMember]
		public float RefRotation { get; set; }

		[YuzuMember]
		public float RefLength { get; set; }

		public Bone()
		{
			RenderChainBuilder = null;
			Length = 100;
			EffectiveRadius = 100;
			FadeoutZone = 50;
			IKStopper = true;
		}

		protected override void SelfLateUpdate(float delta)
		{
			if (Index > 0 && Parent != null) {
				BoneArray.Entry e;
				e.Joint = Position;
				e.Rotation = Rotation;
				e.Length = Length;
				if (BaseIndex > 0) {
					// Tie the bone to the parent bone.
					BoneArray.Entry b = Parent.AsWidget.BoneArray[BaseIndex];
					float l = ClipAboutZero(b.Length);
					Vector2 u = b.Tip - b.Joint;
					Vector2 v = new Vector2(-u.Y / l, u.X / l);
					e.Joint = b.Tip + u * Position.X + v * Position.Y;
					e.Rotation += b.Rotation;
				}
				// Get position of bone's tip.
				e.Tip = Vector2.RotateDegRough(new Vector2(e.Length, 0), e.Rotation) + e.Joint;
				if (RefLength != 0) {
					float relativeScaling = Length / ClipAboutZero(RefLength);
					// Calculating the matrix of relative transformation.
					Matrix32 m1, m2;
					m1 = Matrix32.Transformation(Vector2.Zero, Vector2.One, RefRotation * Mathf.DegToRad, RefPosition);
					m2 = Matrix32.Transformation(Vector2.Zero, new Vector2(relativeScaling, 1), e.Rotation * Mathf.DegToRad, e.Joint);
					e.RelativeTransform = m1.CalcInversed() * m2;
				} else
					e.RelativeTransform = Matrix32.Identity;
				Parent.AsWidget.BoneArray[Index] = e;
				Parent.PropagateDirtyFlags(DirtyFlags.Transform);
			}
		}

		static float ClipAboutZero(float value, float eps = 0.0001f)
		{
			if (value > -eps && value < eps)
				return eps < 0 ? -eps : eps;
			else
				return value;
		}
	}
}
