﻿using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lime
{
	public enum AudioChannelState
	{
		Initial,
		Playing,
		Stopped,
		Paused
	}

	[ProtoContract]
	public enum AudioChannelGroup
	{
		[ProtoEnum]
		Effects,
		[ProtoEnum]
		Music,
		[ProtoEnum]
		Voice
	}

	public interface IAudioChannel
	{
		AudioChannelState State { get; }
		AudioChannelGroup Group { get; set; }
		float Pan { get; set; }
		void Resume(float fadeinTime = 0);
		void Stop(float fadeoutTime = 0);
		float Volume { get; set; }
		float Pitch { get; set; }
		string SamplePath { get; set; }
		Sound Sound { get; }
		void Bump();
	}

	public class NullAudioChannel : IAudioChannel
	{
		public static NullAudioChannel Instance = new NullAudioChannel();

		public AudioChannelState State { get { return AudioChannelState.Stopped; } }
		public AudioChannelGroup Group { get; set; }
		public float Pan { get { return 0; } set { } }
		public void Resume(float fadeinTime = 0) {}
		public void Stop(float fadeoutTime = 0) {}
		public float Volume { get { return 0; } set { } }
		public float Pitch { get { return 1; } set { } }
		public void Bump() {}
		public string SamplePath { get; set; }
		public Sound Sound { get { return null; } }
	}
}
