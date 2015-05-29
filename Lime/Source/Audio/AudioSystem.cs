namespace Lime
{
	/// <summary>
	/// ������������. ��������� ������������� ���� ������
	/// </summary>
	public static class AudioSystem
	{
		static readonly float[] groupVolumes = new float[3] {1, 1, 1};

		public static void Initialize()
		{
			PlatformAudioSystem.Initialize();
		}

		public static void Terminate()
		{
			PlatformAudioSystem.Terminate();
		}

		/// <summary>
		/// ���� ���������� ��� �������� � false, �� ��� ����� ���������� �� �����
		/// </summary>
		public static bool Active
		{
			get { return PlatformAudioSystem.Active; }
			set { PlatformAudioSystem.Active = value; }
		}

		/// <summary>
		/// ���������� ��������� ��� ��������� ������ �������� �������. �� 0 �� 1
		/// </summary>
		public static float GetGroupVolume(AudioChannelGroup group)
		{
			return groupVolumes[(int)group];
		}

		/// <summary>
		/// ������ ��������� ��� ��������� ������ �������� �������. �� 0 �� 1
		/// </summary>
		public static float SetGroupVolume(AudioChannelGroup group, float value)
		{
			float oldVolume = groupVolumes[(int)group];
			value = Mathf.Clamp(value, 0, 1);
			groupVolumes[(int)group] = value;
			PlatformAudioSystem.SetGroupVolume(group, value);
			return oldVolume;
		}

		/// <summary>
		/// ������ �� ����� ��� ����� ��������� ������ �������� �������
		/// </summary>
		public static void PauseGroup(AudioChannelGroup group)
		{
			PlatformAudioSystem.PauseGroup(group);
		}

		/// <summary>
		/// �������� ����� ��� ���� ������ ��������� ������
		/// </summary>
		public static void ResumeGroup(AudioChannelGroup group)
		{
			PlatformAudioSystem.ResumeGroup(group);
		}

		/// <summary>
		/// ������ �� ����� ��� �����
		/// </summary>
		public static void PauseAll()
		{
			PlatformAudioSystem.PauseAll();
		}

		/// <summary>
		/// �������� ����� ��� ���� ������
		/// </summary>
		public static void ResumeAll()
		{
			PlatformAudioSystem.ResumeAll();
		}

		/// <summary>
		/// ��������� ��� ����� � ���, ��� ��� ��� ��� ������ �������������.
		/// �����, ������� ����� �� ���������, ���������������. ������������ ��� ��������� ������,
		/// ���� ���������� ������� ��� ���-�� ����� ������
		/// </summary>
		public static void BumpAll()
		{
			PlatformAudioSystem.BumpAll();
		}

		/// <summary>
		/// ������������� ����� ��������� ������
		/// </summary>
		/// <param name="fadeoutTime">����� �������� ��������� ����� � ��������</param>
		public static void StopGroup(AudioChannelGroup group, float fadeoutTime = 0)
		{
			PlatformAudioSystem.StopGroup(group, fadeoutTime);
		}

		/// <summary>
		/// ��������� ��������� ������������
		/// </summary>
		public static void Update()
		{
			PlatformAudioSystem.Update();
		}

		/// <summary>
		/// ������� ���� � ��������� �����������
		/// </summary>
		/// <param name="path">���� ����� � ������ ������������ ����� Audio (��� ����� ��� ����������)</param>
		/// <param name="group">������ �������� �������� ��� ����� �����</param>
		/// <param name="looping">����������� ������������</param>
		/// <param name="priority">���� �� ����� ������� ����� ��������� ������������, �� ������ � ����� ������ ����������� ����� �������� ������� ������ ����� �����</param>
		/// <param name="fadeinTime">����� �������� ���������� ��������� � ������ ������ ���������������</param>
		/// <param name="paused">���������� ���� ����� �� �����</param>
		/// <param name="volume">���������. �� 0 �� 1</param>
		/// <param name="pan">��������. 0 - �����, 1 - ������, 0.5 - ����������</param>
		/// <param name="pitch">������ �����</param>
		public static Sound Play(string path, AudioChannelGroup group, bool looping = false, float priority = 0.5f, float fadeinTime = 0f, bool paused = false, float volume = 1f, float pan = 0f, float pitch = 1f)
		{
			if (group == AudioChannelGroup.Music && CommandLineArgs.NoMusic) {
				return new Sound();
			}
			return PlatformAudioSystem.Play(path, group, looping, priority, fadeinTime, paused, volume, pan, pitch);
		}

		/// <summary>
		/// ������� ���� � ��������� ����������� � ��������� ��� ������ ������
		/// </summary>
		/// <param name="path">���� ����� � ������ ������������ ����� Audio (��� ����� ��� ����������)</param>
		/// <param name="group">������ �������� �������� ��� ����� �����</param>
		/// <param name="looping">����������� ������������</param>
		/// <param name="priority">���� �� ����� ������� ����� ��������� ������������, �� ������ � ����� ������ ����������� ����� �������� ������� ������ ����� �����</param>
		/// <param name="fadeinTime">����� �������� ���������� ��������� � ������ ������ ���������������</param>
		/// <param name="paused">���������� ���� ����� �� �����</param>
		/// <param name="volume">���������. �� 0 �� 1</param>
		/// <param name="pan">��������. 0 - �����, 1 - ������, 0.5 - ����������</param>
		/// <param name="pitch">������ �����</param>
		public static Sound PlayMusic(string path, bool looping = true, float priority = 100f, float fadeinTime = 0.5f, bool paused = false, float volume = 1f, float pan = 0f, float pitch = 1f)
		{
			return Play(path, AudioChannelGroup.Music, looping, priority, fadeinTime, paused, volume, pan, pitch);
		}

		/// <summary>
		/// ������� ���� � ��������� ����������� � ��������� ��� ������ ��������
		/// </summary>
		/// <param name="path">���� ����� � ������ ������������ ����� Audio (��� ����� ��� ����������)</param>
		/// <param name="group">������ �������� �������� ��� ����� �����</param>
		/// <param name="looping">����������� ������������</param>
		/// <param name="priority">���� �� ����� ������� ����� ��������� ������������, �� ������ � ����� ������ ����������� ����� �������� ������� ������ ����� �����</param>
		/// <param name="fadeinTime">����� �������� ���������� ��������� � ������ ������ ���������������</param>
		/// <param name="paused">���������� ���� ����� �� �����</param>
		/// <param name="volume">���������. �� 0 �� 1</param>
		/// <param name="pan">��������. 0 - �����, 1 - ������, 0.5 - ����������</param>
		/// <param name="pitch">������ �����</param>
		public static Sound PlayEffect(string path, bool looping = false, float priority = 0.5f, float fadeinTime = 0f, bool paused = false, float volume = 1f, float pan = 0f, float pitch = 1f)
		{
			return Play(path, AudioChannelGroup.Effects, looping, priority, fadeinTime, paused, volume, pan, pitch);
		}
	}
}