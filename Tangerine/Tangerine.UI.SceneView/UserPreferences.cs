using System;
using Lime;
using Yuzu;

namespace Tangerine.UI.SceneView
{
	public class UserPreferences : Component
	{
		[YuzuRequired]
		public bool ShowOverlays { get; set; }
	}
}