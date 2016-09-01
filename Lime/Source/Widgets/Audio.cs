using Yuzu;

namespace Lime
{
	public enum AudioAction
	{
		Play,
		Stop
	}

	public class Audio : Node
	{
		Sound sound = new Sound() { IsBumpable = true };

		[YuzuMember]
		public SerializableSample Sample { get; set; }

		/// <summary>
		/// ����������� ������������
		/// </summary>
		[YuzuMember]
		public bool Looping { get; set; }

		/// <summary>
		/// ����� ��������� � ��������
		/// </summary>
		[YuzuMember]
		public float FadeTime { get; set; }

		private float volume = 0.5f;

		/// <summary>
		/// ��������� (0 - 1)
		/// </summary>
		[YuzuMember]
		public float Volume
		{
			get { return volume; }
			set
			{
				volume = value;
				sound.Volume = volume;
			}
		}

		private float pan = 0;

		/// <summary>
		/// ����� �����/������ (-1 - �����, 1 - ������, 0 - ����������)
		/// </summary>
		[YuzuMember]
		public float Pan
		{
			get { return pan; }
			set
			{
				pan = value;
				sound.Pan = pan;
			}
		}

		private float pitch = 1;

		/// <summary>
		/// ������ �����
		/// </summary>
		[YuzuMember]
		public float Pitch
		{
			get { return pitch; }
			set
			{
				pitch = value;
				sound.Pitch = pitch;
			}
		}

		[Trigger]
		public AudioAction Action { get; set; }

		/// <summary>
		/// ������ ������. �������� �����, ������� ����, ������. ������ ��������� �������� ����� ��������� ��� ���� ������ ������
		/// </summary>
		[YuzuMember]
		public AudioChannelGroup Group { get; set; }

		[YuzuMember]
		public float Priority { get; set; }

		[YuzuMember]
		public bool Bumpable { get; set; }

		public Audio()
		{
			Priority = 0.5f;
			Bumpable = true;
		}

		public void Play()
		{
			sound = Sample.Play(Group, false, 0f, Looping, Priority, Volume, Pan, Pitch);
			sound.IsBumpable = Bumpable;
		}

		public void Stop()
		{
			sound.Stop(FadeTime);
		}

		public bool IsPlaying()
		{
			return !sound.IsStopped;
		}

		protected override void SelfUpdate(float delta)
		{
			sound.Bump();
		}

		public override void OnTrigger(string property)
		{
			if (property == "Action") {
				if (Action == AudioAction.Play) {
					Play();
				} else {
					Stop();
				}
			} else {
				base.OnTrigger(property);
			}
		}
	}
}
