using Yuzu;

namespace Lime
{
	/// <summary>
	/// ��������������� �����, �������������� ��� ������������ ������ ����� ProtoBuf
	/// </summary>
	public class SerializableSample
	{
		public string Path;

		public SerializableSample() {}

		public SerializableSample(string path)
		{
			SerializationPath = path;
		}

		[YuzuMember]
		public string SerializationPath
		{
			get { return Serialization.ShrinkPath(Path); }
			set { Path = Serialization.ExpandPath(value); }
		}

		/// <summary>
		/// ������� ���� � ��������� �����������
		/// </summary>
		/// <param name="group">������ �������� �������� ��� ����� �����</param>
		/// <param name="looping">����������� ������������</param>
		/// <param name="priority">���� �� ����� ������� ����� ��������� ������������, �� ������ � ����� ������ ����������� ����� �������� ������� ������ ����� �����</param>
		/// <param name="fadeinTime">����� �������� ���������� ��������� � ������ ������ ���������������</param>
		/// <param name="paused">���������� ���� ����� �� �����</param>
		/// <param name="volume">���������. �� 0 �� 1</param>
		/// <param name="pan">��������. 0 - �����, 1 - ������, 0.5 - ����������</param>
		/// <param name="pitch">������ �����</param>
		public Sound Play(AudioChannelGroup group, bool paused, float fadeinTime = 0, bool looping = false, float priority = 0.5f, float volume = 1, float pan = 0, float pitch = 1)
		{
			return AudioSystem.Play(Path, group, looping, priority, fadeinTime, paused, volume, pan, pitch);
		}
	}
}