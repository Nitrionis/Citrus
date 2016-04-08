#if OPENAL
using System;
using System.Collections.Generic;
using System.Linq;
#if ANDROID
using System.Runtime.InteropServices;
#endif
using System.Threading;

using OpenTK;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;

#if iOS
using Foundation;
using AVFoundation;
#endif

namespace Lime
{
	public static class PlatformAudioSystem
	{
#if ANDROID
		const string Lib = "openal32";
		const CallingConvention Style = CallingConvention.Cdecl;

		[DllImport(Lib, EntryPoint = "alcDevicePauseSOFT", ExactSpelling = true, CallingConvention = Style)]
		unsafe static extern void AlcDevicePauseSoft(IntPtr device);

		[DllImport(Lib, EntryPoint = "alcDeviceResumeSOFT", ExactSpelling = true, CallingConvention = Style)]
		unsafe static extern void AlcDeviceResumeSoft(IntPtr device);
#endif

		public struct ErrorChecker : IDisposable
		{
			string comment;
			bool throwException;

			public ErrorChecker(string comment = null, bool throwException = true)
			{
				this.comment = comment;
				this.throwException = throwException;
				// Clear current error
				AL.GetError();
			}

			void IDisposable.Dispose()
			{
				var error = AL.GetError();
				if (error != ALError.NoError) {
					string message = "OpenAL error: " + AL.GetErrorString(error);
					if (comment != null) {
						message += string.Format(" ({0})", comment);
					}
					if (throwException) {
						throw new Exception(message);
					} else {
						Logger.Write(message);
					}
				}
			}
		}

		static readonly List<AudioChannel> channels = new List<AudioChannel>();
		static AudioContext context;
		static Thread streamingThread = null;
		static volatile bool shouldTerminateThread;
#if iOS
		static NSObject interruptionNotification;
#endif

		public static void Initialize(ApplicationOptions options)
		{
#if iOS
			AVAudioSession.SharedInstance().Init();
			AVAudioSession.SharedInstance().SetCategory(AVAudioSessionCategory.Ambient);

			interruptionNotification = AVAudioSession.Notifications.ObserveInterruption((sender, args) => {
				if (args.InterruptionType == AVAudioSessionInterruptionType.Began) {
					Active = false;
					AVAudioSession.SharedInstance().SetActive(false);
				} else if (args.InterruptionType == AVAudioSessionInterruptionType.Ended) {

					// Grisha: Workaround on "AUIOClient_StartIO failed" issue.
					// On iOS sound not restores after incoming call.
					// Making everything like in tutorial doesn't help much.
					// So, we wait a bit before restoring - and it works.
					Thread.Sleep(500);

					AVAudioSession.SharedInstance().SetCategory(AVAudioSessionCategory.Ambient);
					AVAudioSession.SharedInstance().SetActive(true);
					Active = true;
				}
			});
			context = new AudioContext();
#elif ANDROID
			// LoadLibrary() ivokes JNI_OnLoad()
			Java.Lang.JavaSystem.LoadLibrary(Lib);
			context = new AudioContext();
#else
			bool isDeviceAvailable = !String.IsNullOrEmpty(AudioContext.DefaultDevice);
			if (isDeviceAvailable && !CommandLineArgs.NoAudio) {
				context = new AudioContext();
			}
#endif
			if (AL.GetError() == ALError.NoError) {
				// iOS dislike to mix stereo and mono buffers on one audio source, so separate them
				for (int i = 0; i < options.NumStereoChannels; i++) {
					channels.Add(new AudioChannel(i, AudioFormat.Stereo16));
				}
				for (int i = 0; i < options.NumMonoChannels; i++) {
					channels.Add(new AudioChannel(i, AudioFormat.Mono16));
				}
			}
			if (options.DecodeAudioInSeparateThread) {
				streamingThread = new Thread(RunStreamingLoop);
				streamingThread.IsBackground = true;
				streamingThread.Start();
			}
		}

		public static bool Active
		{
			get { return context != null && Alc.GetCurrentContext().Handle != IntPtr.Zero; }
			set
			{
				if (Active == value) {
					return;
				}
				SetActive(value);
			}
		}

#if ANDROID
		private static void SetActive(bool value)
		{
			if (value) {
				if (context != null) {
					try {
						context.MakeCurrent();
					} catch (AudioContextException) {
						Logger.Write("Error: failed to resume OpenAL after interruption ended");
					}
				}
				AlcDeviceResumeSoft(Alc.GetContextsDevice(Alc.GetCurrentContext()));
			} else {
				AlcDevicePauseSoft(Alc.GetContextsDevice(Alc.GetCurrentContext()));
				Alc.MakeContextCurrent(ContextHandle.Zero);
			}
		}
#elif iOS
		private static void SetActive(bool value)
		{
			if (value) {
				if (context != null) {
					try {
						context.MakeCurrent();
						context.Process();
					} catch (AudioContextException) {
						Logger.Write("Error: failed to resume OpenAL after interruption ended");
					}
				}
			} else {
				if (context != null) {
					context.Suspend();
				}
				Alc.MakeContextCurrent(ContextHandle.Zero);
			}
		}
#else
		private static void SetActive(bool value)
		{
			if (value) {
				if (context != null) {
					try {
						context.MakeCurrent();
					} catch (AudioContextException) {
						Logger.Write("Error: failed to resume OpenAL after interruption ended");
					}
				}
				ResumeAll();
			} else {
				PauseAll();
				Alc.MakeContextCurrent(ContextHandle.Zero);
			}
		}
#endif

		public static void Terminate()
		{
			if (streamingThread != null) {
				shouldTerminateThread = true;
				streamingThread.Join();
				streamingThread = null;
			}
			foreach (var channel in channels) {
				channel.Dispose();
			}
			channels.Clear();
			if (context != null) {
				context.Dispose();
				context = null;
			}
#if iOS
			if (interruptionNotification != null) {
				interruptionNotification.Dispose();
				interruptionNotification = null;
			}
#endif
		}

		private static long tickCount;

		private static long GetTimeDelta()
		{
			long delta = (DateTime.Now.Ticks / 10000L) - tickCount;
			if (tickCount == 0) {
				tickCount = delta;
				delta = 0;
			} else {
				tickCount += delta;
			}
			return delta;
		}

		static void RunStreamingLoop()
		{
			while (!shouldTerminateThread) {
				UpdateChannels();
				Thread.Sleep(10);
			}
		}

		public static void Update()
		{
			if (streamingThread == null) {
				UpdateChannels();
			}
		}

		private static void UpdateChannels()
		{
			float delta = GetTimeDelta() * 0.001f;
			foreach (var channel in channels) {
				channel.Update(delta);
			}
		}

		public static void SetGroupVolume(AudioChannelGroup group, float value)
		{
			foreach (var channel in channels) {
				if (channel.Group == group) {
					channel.Volume = channel.Volume;
				}
			}
		}

		public static void PauseGroup(AudioChannelGroup group)
		{
			foreach (var channel in channels) {
				if (channel.Group == group && channel.State == AudioChannelState.Playing) {
					channel.Pause();
				}
			}
		}

		public static void ResumeGroup(AudioChannelGroup group)
		{
			foreach (var channel in channels) {
				if (channel.Group == group && channel.State == AudioChannelState.Paused) {
					channel.Resume();
				}
			}
		}

		public static void PauseAll()
		{
			foreach (var channel in channels) {
				if (channel.State == AudioChannelState.Playing) {
					channel.Pause();
				}
			}
		}

		public static void ResumeAll()
		{
			foreach (var channel in channels) {
				if (channel.State == AudioChannelState.Paused) {
					channel.Resume();
				}
			}
		}

		public static void BumpAll()
		{
			foreach (var channel in channels) {
				channel.Bump();
			}
		}

		public static void StopGroup(AudioChannelGroup group, float fadeoutTime)
		{
			foreach (var channel in channels) {
				if (channel.Group == group) {
					channel.Stop(fadeoutTime);
				}
			}
		}

		delegate AudioChannel ChannelSelector(AudioFormat format);

		static readonly AudioCache cache = new AudioCache();

		private static Sound LoadSoundToChannel(
			ChannelSelector channelSelector, string path, bool looping, bool paused, float fadeinTime)
		{
			if (context == null) {
				return new Sound();
			}
			path += ".sound";
			var sound = new Sound();
			var stream = cache.OpenStream(path);
			if (stream == null) {
				return sound;
			}
			var decoder = AudioDecoderFactory.CreateDecoder(stream);
			var channel = channelSelector(decoder.GetFormat());
			if (channel == null || !channel.Play(sound, decoder, looping, paused, fadeinTime)) {
				decoder.Dispose();
				return sound;
			}
			channel.SamplePath = path;
			return sound;
		}

		private static AudioChannel AllocateChannel(float priority, AudioFormat format)
		{
			var channels = PlatformAudioSystem.channels.Where(c => c.AudioFormat == format).ToList();
			channels.Sort((a, b) => {
				if (a.Priority != b.Priority) {
					return Mathf.Sign(a.Priority - b.Priority);
				}
				if (a.StartupTime == b.StartupTime) {
					return a.Id - b.Id;
				}
				return (a.StartupTime < b.StartupTime) ? -1 : 1;
			});
			// Looking for stopped channels
			foreach (var channel in channels) {
				if (channel.Streaming) {
					continue;
				}
				var state = channel.State;
				if (state == AudioChannelState.Stopped || state == AudioChannelState.Initial) {
					return channel;
				}
			}
			// Trying to stop first channel in order of priority
			foreach (var channel in channels) {
				if (channel.Priority <= priority) {
					channel.Stop();
					if (channel.State == AudioChannelState.Stopped) {
						return channel;
					}
				}
			}
			return null;
		}

		public static Sound Play(
			string path,
			AudioChannelGroup group,
			bool looping = false,
			float priority = 0.5f,
			float fadeinTime = 0f,
			bool paused = false,
			float volume = 1f,
			float pan = 0f,
			float pitch = 1f)
		{
			ChannelSelector channelSelector = (format) => {
				var channel = AllocateChannel(priority, format);
				if (channel != null) {
					if (channel.Sound != null) {
						channel.Sound.Channel = NullAudioChannel.Instance;
					}
					channel.Group = group;
					channel.Priority = priority;
					channel.Volume = volume;
					channel.Pitch = pitch;
					channel.Pan = pan;
				}
				return channel;
			};
			var sound = LoadSoundToChannel(channelSelector, path, looping, paused, fadeinTime);
			return sound;
		}
	}
}
#endif
