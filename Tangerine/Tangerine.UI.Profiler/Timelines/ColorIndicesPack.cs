#if PROFILER

namespace Tangerine.UI.Timelines
{
	internal struct ColorIndicesPack
	{
		public const int MaxColorsCount = 8;

		private const uint BehaviorBitMask = 1u << MaxColorsCount;
			
		private uint InternalData;

		/// <summary>
		/// Interprets structure data as a set of indices.
		/// </summary>
		/// <param name="rasterizationTarget">
		/// An array into which the color indices will be written.
		/// </param>
		/// <returns>
		/// Number of colors in the pack.
		/// </returns>
		public uint RasterizeTo(uint[] rasterizationTarget)
		{
			uint colorSlotIndex = 0;
			if ((InternalData & BehaviorBitMask) != 0) {
				uint dataCopy = InternalData;
				for (uint i = 0; i < MaxColorsCount; i++, dataCopy <<= 1) {
					uint bit = dataCopy & 1u;
					rasterizationTarget[colorSlotIndex] = i * bit;
					colorSlotIndex += bit;
				}
				return colorSlotIndex;
			} else {
				return InternalData;
			}
		}

		public static ColorIndicesPack SingleColor(byte colorIndex) => 
			new ColorIndicesPack { InternalData = colorIndex };
			
		public static ColorIndicesPack EachBitAsColor(byte colors)
		{
			var pack = new ColorIndicesPack();
			pack.InternalData |= colors;
			pack.InternalData |= BehaviorBitMask;
			return pack;
		}
	}
}

#endif // PROFILER