using System;
using System.Collections.Generic;
using System.Diagnostics;
using Yuzu;

namespace Lime
{
	public struct Quadrangle
	{
		public Vector2 V1;
		public Vector2 V2;
		public Vector2 V3;
		public Vector2 V4;

		public Vector2 this[int index]
		{
			get
			{
				if (index == 0) return V1;
				if (index == 1) return V2;
				if (index == 2) return V3;
				if (index == 3) return V4;
				throw new IndexOutOfRangeException();
			}
			set
			{
				if (index == 0) V1 = value;
				if (index == 1) V2 = value;
				if (index == 2) V3 = value;
				if (index == 3) V4 = value;
				throw new IndexOutOfRangeException();
			}
		}

		/// <summary>
		/// Returns true if the quadrangles have common points.
		/// </summary>
		public bool Overlaps(Quadrangle quadrangle)
		{
			for (int k = 0; k < 2; k++) {
				var outerQuad = k == 0 ? this : quadrangle;
				var innerQuad = k == 0 ? quadrangle : this;
				var sign = Vector2.CrossProduct(outerQuad[1] - outerQuad[0], outerQuad[3] - outerQuad[0]);
				if (sign.Abs() < Mathf.ZeroTolerance) {
					return false;
				}
				for (int i = 0; i < 4; i++) {
					var a = outerQuad[i];
					var b = outerQuad[(i + 1) % 4];
					var allOutside = true;
					for (int j = 0; j < 4; j++) {
						allOutside &= GeometryUtils.CalcPointHalfPlane(innerQuad[j], a, b) * sign < 0;
					}
					if (allOutside) {
						return false;
					}
				}
			}
			return true;
		}

		/// <summary>
		/// Returns true if one side of the first quadrangle intersects a side of the second quadrangle.
		/// </summary>
		public bool Intersects(Quadrangle quadrangle)
		{
			for (int i = 0; i < 4; i++) {
				var a1 = this[i];
				var b1 = this[(i + 1) % 4];
				for (int j = 0; j < 4; j++) {
					var a2 = quadrangle[j];
					var b2 = quadrangle[(j + 1) % 4];
					Vector2 intersection;
					if (GeometryUtils.CalcLinesIntersection(a1, b1, a2, b2, out intersection)) {
						return true;
					}
				}
			}
			return false;
		}
	}
}