#if OPENAL
#if !MONOMAC
using OpenTK.Audio.OpenAL;
#else
using MonoMac.OpenAL;
#endif
#endif

namespace Lime
{
	/// <summary>
	/// ����. ������ ��� ��������� ����������������, ���������, ��� �� ���������� (����������� �� ������� ������).
	/// ��� ����� ���������� �������� IsLoading
	/// </summary>
	public class Sound
	{
		/// <summary>
		/// �����������. ���������� ���� ������������� �� �����, ������ ��� � �������� ��������� ������ ��� ���������� NullAudioChannel.
		/// ����������� AudioSystem.Play ��� �������� �������� � ������������ ������
		/// </summary>
		public Sound()
		{
			Channel = NullAudioChannel.Instance;
		}

		/// <summary>
		/// ����������, �������� ����������� ���� ����
		/// </summary>
		public IAudioChannel Channel { get; internal set; }

		/// <summary>
		/// ��� ����� ��������� �������� Bump
		/// </summary>
		public bool IsBumpable { get; set; }

		/// <summary>
		/// ���������� true, ���� ���� ��� ��� �����������
		/// </summary>
		public bool IsLoading { get; internal set; }

		/// <summary>
		/// ���������� true, ���� ���� ��������� �� �����, ���� ��� ���������� ������� Stop
		/// </summary>
		public bool IsStopped { get { return Channel.State == AudioChannelState.Stopped; } }

		/// <summary>
		/// ���������. �� 0 �� 1.
		/// </summary>
		public float Volume
		{
			get { return Channel.Volume; }
			set { Channel.Volume = value; }
		}

		/// <summary>
		/// ������ �����
		/// </summary>
		public float Pitch
		{
			get { return Channel.Pitch; }
			set { Channel.Pitch = value; }
		}

		/// <summary>
		/// ��������. -1 - �����, 1 - ������, 0 - ����������
		/// </summary>
		public float Pan
		{
			get { return Channel.Pan; }
			set { Channel.Pan = value; }
		}

		/// <summary>
		/// ��������� ���� � ������� ��� ��������� ������� Stop
		/// </summary>
		/// <param name="fadeinTime">����� �������� ���������� ��������� � ��������</param>
		public void Resume(float fadeinTime = 0)
		{
			EnsureLoaded();
			Channel.Resume(fadeinTime);
		}

		/// <summary>
		/// ���������� ������������ �����
		/// </summary>
		/// <param name="fadeoutTime">����� �������� ���������� ��������� � ��������</param>
		public void Stop(float fadeoutTime = 0)
		{
			EnsureLoaded();
			Channel.Stop(fadeoutTime);
		}

		/// <summary>
		/// ���������� ����, ��� �� ��� ��� �����. �����, ������� ����� �� ���������, ������������� ���������������
		/// </summary>
		public void Bump() { Channel.Bump(); }

		private void EnsureLoaded()
		{
			if (IsLoading) {
				throw new System.InvalidOperationException("The sound is being loaded");
			}
		}
	}
}
