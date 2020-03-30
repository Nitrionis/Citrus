using System;
using System.Collections.Generic;
using IPEndPoint = System.Net.IPEndPoint;
using Lime.Graphics.Platform;
using GpuHistory = Lime.Graphics.Platform.ProfilerHistory;
using Yuzu;

namespace Lime.Profilers.Contexts
{
	public abstract class NetworkContext : Context
	{
		internal Network.NetworkMember networkMember;

		public bool IsConnected => networkMember.IsConnected;

		public Action Closed
		{
			get => networkMember.Closed;
			set => networkMember.Closed += value;
		}

		public abstract bool TryLaunch(IPEndPoint ipEndPoint);

		public class Message : Network.IItem
		{
			[YuzuRequired]
			public bool IsEmpty { get; set; }

			[YuzuRequired]
			public bool IsCloseRequested { get; set; }
		}

		public class ProfilerOptions
		{
			public enum State
			{
				Nothing,
				True,
				False
			}

			[YuzuMember]
			public State ProfilingEnabled;

			[YuzuMember]
			public State DrawCallsRenderTimeEnabled;

			[YuzuMember]
			public State SceneOnlyDrawCallsRenderTime;

			public static bool HasField(State fieldValue) => fieldValue != State.Nothing;

			public static State StateOf(bool value) => value ? State.True : State.False;

			public static bool StateToBool(State fieldValue) => fieldValue == State.True;
		}

		/// <summary>
		/// Request to execute a command or get additional data.
		/// </summary>
		/// <remarks>
		/// Designed on the basis of the fact that the request is a rare event.
		/// That is, between requests, the client will have time to send statistics several times.
		/// </remarks>
		public class Request : Message
		{
			public enum State
			{
				Nothing,
				True,
				False
			}

			[YuzuMember]
			public ProfilerOptions Options;

			/// <summary>
			/// The index of the frame for which detailed information is needed.
			/// </summary>
			[YuzuMember]
			public long FrameIndex = -1;

			[YuzuMember]
			public bool GpuDrawCallsResultsForFrame = false;
		}

		/// <summary>
		/// Response for <see cref="Request"/>.
		/// </summary>
		public class Response
		{
			[YuzuMember]
			public long FrameIndex;

			[YuzuMember]
			public List<ProfilingResult> DrawCalls;
		}

		protected class Statistics : Message
		{
			[YuzuMember]
			public ProfilerOptions Options;

			[YuzuMember]
			public GpuHistory.Item Frame;

			[YuzuMember]
			public CpuHistory.Item Update;

			[YuzuMember]
			public Response Response;
		}
	}
}
