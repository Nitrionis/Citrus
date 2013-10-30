using System;
using ProtoBuf;
using System.Collections.Generic;

namespace Lime
{
	[ProtoContract]
	[ProtoInclude(100, typeof(SerializableTexture))]
	public interface ITexture : IDisposable
	{
		Size ImageSize { get; }
		Size SurfaceSize { get; }
		Rectangle UVRect { get; }
		uint GetHandle();
		void SetAsRenderTarget();
		void RestoreRenderTarget();
		bool IsTransparentPixel(int x, int y);
		bool IsStubTexture { get; }
		[ProtoMember(1)]
		string SerializationPath { get; set; }
#if UNITY
		UnityEngine.Texture GetUnityTexture();
#endif
	}
}
