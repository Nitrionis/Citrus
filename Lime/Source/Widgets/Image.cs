using System;
using Lime;
using ProtoBuf;

namespace Lime
{
	[ProtoContract]
	public class Image : Widget, IImageCombinerArg
	{
		bool skipRender;
		bool requestSkipRender;

		[ProtoMember(1)]
		public override sealed ITexture Texture { get; set; }

		[ProtoMember(2)]
		public Vector2 UV0 { get; set; }

		[ProtoMember(3)]
		public Vector2 UV1 { get; set; }

		public Image()
		{
			UV0 = Vector2.Zero;
			UV1 = Vector2.One;
			Texture = new SerializableTexture();
		}

		public Image(ITexture texture)
		{
			UV0 = Vector2.Zero;
			UV1 = Vector2.One;
			Texture = texture;
			Size = (Vector2)texture.ImageSize;
		}

		public Image(string texturePath)
		{
			UV0 = Vector2.Zero;
			UV1 = Vector2.One;
			Texture = new SerializableTexture(texturePath);
			Size = (Vector2)Texture.ImageSize;
		}

		public override Vector2 CalcContentSize()
		{
			return (Vector2)Texture.ImageSize;
		}

		public override void Render()
		{
			Renderer.Blending = GlobalBlending;
			Renderer.Transform1 = LocalToWorldTransform;
			Renderer.DrawSprite(Texture, GlobalColor, Vector2.Zero, Size, UV0, UV1);
		}

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

		public override void Update(int delta)
		{
			skipRender = requestSkipRender;
			requestSkipRender = false;
			base.Update(delta);
		}

		public override bool HitTest(Vector2 point)
		{
			if (GloballyVisible && !skipRender) {
				if (HitTestMethod == HitTestMethod.Contents) {
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
					} else
						return false;
				} else
					return base.HitTest(point);
			}
			return false;
		}
	}
}
