using ProtoBuf;
using System;

namespace Lime
{
	/// <summary>
	/// ������������ ������� �������� � ���������. ������������ ��� �������� ��������� ��������
	/// </summary>
	[System.Diagnostics.DebuggerStepThrough]
	[ProtoContract]
	public struct NumericRange : IEquatable<NumericRange>
	{
		/// <summary>
		/// ������� ��������
		/// </summary>
		[ProtoMember(1)]
		public float Median;

		/// <summary>
		/// ���������� �� �������� ��������
		/// </summary>
		[ProtoMember(2)]
		public float Dispersion;

		/// <summary>
		/// �����������
		/// </summary>
		/// <param name="median">������� ��������</param>
		/// <param name="variation">���������� �� �������� ��������</param>
		public NumericRange(float median, float variation)
		{
			Median = median;
			Dispersion = variation;
		}

		/// <summary>
		/// ���������� ��������� �����, ���������� � ������ �������� �������� � ���������
		/// ��������� �� ����� ���� ������ �������� ��������
		/// </summary>
		public float NormalRandomNumber()
		{
			return Mathf.NormalRandom(Median, Dispersion);
		}

		public float NormalRandomNumber(System.Random rng)
		{
			return rng.NormalRandom(Median, Dispersion);
		}

		/// <summary>
		/// ���������� ��������� �����, ���������� � ������ �������� �������� � ���������
		/// </summary>
		public float UniformRandomNumber()
		{
			return Mathf.UniformRandom(Median, Dispersion);
		}

		public float UniformRandomNumber(System.Random rng)
		{
			return rng.UniformRandom(Median, Dispersion);
		}

		bool IEquatable<NumericRange>.Equals(NumericRange rhs)
		{
			return Median == rhs.Median && Dispersion == rhs.Dispersion;
		}

		public override string ToString()
		{
			return String.Format("{0}, {1}", Median, Dispersion);
		}
	}
}
