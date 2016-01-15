using System;
using Lime;
using ProtoBuf;

namespace Lime
{
	/// <summary>
	/// ������, ���������� � ���� �����������
	/// </summary>
	[ProtoContract]
	public class Image : Widget, IImageCombinerArg, IPresenter
	{
		bool skipRender;
		bool requestSkipRender;

		/// <summary>
		/// �����������, ������������ ���� ��������
		/// </summary>
		[ProtoMember(1)]
		public override sealed ITexture Texture { get; set; }

		/// <summary>
		/// ���������� ���������� ������ �������� ���� ��������
		/// </summary>
		[ProtoMember(2)]
		public Vector2 UV0 { get; set; }

		/// <summary>
		/// ���������� ���������� ������� ������� ���� ��������
		/// </summary>
		[ProtoMember(3)]
		public Vector2 UV1 { get; set; }

		public Image()
		{
			UV1 = Vector2.One;
			HitTestMethod = HitTestMethod.Contents;
			Texture = new SerializableTexture();
			Presenter = this;
		}

		public Image(ITexture texture)
		{
			UV1 = Vector2.One;
			Texture = texture;
			HitTestMethod = HitTestMethod.Contents;
			Size = (Vector2)texture.ImageSize;
			Presenter = this;
		}

		public Image(string texturePath)
			: this(new SerializableTexture(texturePath))
		{
		}

		/// <summary>
		/// ���������� ������ ��������
		/// </summary>
		public override Vector2 CalcContentSize()
		{
			return (Vector2)Texture.ImageSize;
		}

		/// <summary>
		/// ��������� ������ � ��� ��� �������� ������� � ������� ���������
		/// </summary>
		public override void AddToRenderChain(RenderChain chain)
		{
			if (GloballyVisible && !skipRender) {
				chain.Add(this, Layer);
			}
		}

		ITexture IImageCombinerArg.GetTexture()
		{
			return Texture;
		}

		void IImageCombinerArg.SkipRender()
		{
			requestSkipRender = true;
		}

		Matrix32 IImageCombinerArg.UVTransform
		{
			get { return new Matrix32(new Vector2(UV1.X - UV0.X, 0), new Vector2(0, UV1.Y - UV0.Y), UV0); }
		}

		protected override void SelfUpdate(float delta)
		{
			skipRender = requestSkipRender;
			requestSkipRender = false;
		}

		protected override bool SelfHitTest(Vector2 point)
		{
			if (!GloballyVisible || skipRender || !InsideClipRect(point)) {
				return false;
			}
			if (HitTestMethod != HitTestMethod.Contents) {
				return base.SelfHitTest(point);
			}
			Vector2 localPoint = LocalToWorldTransform.CalcInversed().TransformVector(point);
			Vector2 size = Size;
			if (size.X < 0) {
				localPoint.X = -localPoint.X;
				size.X = -size.X;
			}
			if (size.Y < 0) {
				localPoint.Y = -localPoint.Y;
				size.Y = -size.Y;
			}
			if (localPoint.X >= 0 && localPoint.Y >= 0 && localPoint.X < size.X && localPoint.Y < size.Y) {
				int u = (int)(Texture.ImageSize.Width * (localPoint.X / size.X));
				int v = (int)(Texture.ImageSize.Height * (localPoint.Y / size.Y));
				return !Texture.IsTransparentPixel(u, v);
			} else {
				return false;
			}
		}

		void IPresenter.Render()
		{
			PrepareRendererState();
			Renderer.DrawSprite(Texture, GlobalColor, ContentPosition, ContentSize, UV0, UV1);
		}

		IPresenter IPresenter.Clone(Node newNode)
		{
			return (IPresenter)newNode;
		}
	}
}