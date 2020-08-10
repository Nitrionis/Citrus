using System.Collections.Generic;

namespace Lime.Profiler.Graphics
{
	internal class RenderThreadProfilingInfo
	{
		private static readonly Stack<bool> states = new Stack<bool>();

		static RenderThreadProfilingInfo() => states.Push(false);

		/// <summary>
		/// True if the current command block belongs to the scene.
		/// </summary>
		public static bool IsInsideOfScene => states.Peek();

		public static void PushState(bool value) => states.Push(value);

		public static void PopState() => states.Pop();
	}
}
