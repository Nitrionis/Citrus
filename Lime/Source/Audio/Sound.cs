#if OPENAL
using OpenTK.Audio.OpenAL;
#endif

namespace Lime
{
	public class Sound
	{
		public Sound()
		{
			Channel = NullAudioChannel.Instance;
		}

		public IAudioChannel Channel { get; internal set; }
		public bool IsBumpable { get; set; }
		public bool IsLoading { get; internal set; }
		public bool IsStopped { get { return Channel.State == AudioChannelState.Stopped; } }
		
		public float Volume
		{
			get { return Channel.Volume; }
			set { Channel.Volume = value; }
		}

		public float Pitch
		{
			get { return Channel.Pitch; }
			set { Channel.Pitch = value; }
		}

		public float Pan
		{
			get { return Channel.Pan; }
			set { Channel.Pan = value; }
		}

		public void Resume(float fadeinTime = 0)
		{
			EnsureLoaded();
			Channel.Resume(fadeinTime);
		}

		public void Stop(float fadeoutTime = 0)
		{
			EnsureLoaded();
			Channel.Stop(fadeoutTime);
		}

		public void Bump() { Channel.Bump(); }

		private void EnsureLoaded()
		{
			if (IsLoading) {
				throw new System.InvalidOperationException("The sound is being loaded");
			}
		}
	}
}
