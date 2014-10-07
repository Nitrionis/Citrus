// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DownloadNotification.cs" company="Matthew Leibowitz">
//   Copyright (c) Matthew Leibowitz
//   This code is licensed under the Apache 2.0 License
//   http://www.apache.org/licenses/LICENSE-2.0.html
// </copyright>
// <summary>
//   The download notification.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ExpansionDownloader
{
    using Android;
    using Android.App;
    using Android.Content;
    using Android.OS;
    using Android.Runtime;

    using ExpansionDownloader.Client;
    using ExpansionDownloader.Service;

    using Java.Lang;

    /// <summary>
    /// The download notification.
    /// </summary>
    public class DownloadNotification : IDownloaderClient
    {
        #region Static Fields

        /// <summary>
        /// The notification id.
        /// </summary>
        private static readonly int NotificationId = typeof(DownloadNotification).GetHashCode();

        #endregion

        #region Fields

        /// <summary>
        /// The m context.
        /// </summary>
        private readonly Context context;

        /// <summary>
        /// The m label.
        /// </summary>
        private readonly string label;

        /// <summary>
        /// The m notification.
        /// </summary>
        private readonly Notification notification;

        /// <summary>
        /// The m notification manager.
        /// </summary>
        private readonly NotificationManager notificationManager;

        /// <summary>
        /// The m client proxy.
        /// </summary>
        private IDownloaderClient clientProxy;

        /// <summary>
        /// The m state.
        /// </summary>
        private DownloaderState clientState;

        /// <summary>
        /// The m current notification.
        /// </summary>
        private Notification currentNotification;

        /// <summary>
        /// The m current text.
        /// </summary>
        private string currentText;

        /// <summary>
        /// The m current title.
        /// </summary>
        private string currentTitle;

        /// <summary>
        /// The m progress info.
        /// </summary>
        private DownloadProgressInfo progressInfo;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DownloadNotification"/> class.
        /// </summary>
        /// <param name="ctx">
        /// The ctx.
        /// </param>
        /// <param name="applicationLabel">
        /// The application label.
        /// </param>
        internal DownloadNotification(Context ctx, string applicationLabel)
        {
            this.clientState = DownloaderState.Unknown;
            this.context = ctx;
            this.label = applicationLabel;
            this.notificationManager =
                this.context.GetSystemService(Context.NotificationService).JavaCast<NotificationManager>();
            this.notification = new Notification();
            this.currentNotification = this.notification;
        }

        #endregion

        #region Interfaces

        /// <summary>
        /// The custom notification.
        /// </summary>
        public interface ICustomNotification
        {
            #region Public Properties

            /// <summary>
            /// Sets CurrentBytes.
            /// </summary>
            long CurrentBytes { set; }

            /// <summary>
            /// Sets Icon.
            /// </summary>
            int Icon { set; }

            /// <summary>
            /// Sets PausedText.
            /// </summary>
            string PausedText { set; }

            /// <summary>
            /// Sets PendingIntent.
            /// </summary>
            PendingIntent PendingIntent { set; }

            /// <summary>
            /// Sets Ticker.
            /// </summary>
            string Ticker { set; }

            /// <summary>
            /// Sets TimeRemaining.
            /// </summary>
            long TimeRemaining { set; }

            /// <summary>
            /// Sets Title.
            /// </summary>
            string Title { set; }

            /// <summary>
            /// Sets TotalBytes.
            /// </summary>
            long TotalBytes { set; }

            #endregion

            #region Public Methods and Operators

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="context">
            /// The context to use to obtain access to the Notification Service
            /// </param>
            /// <returns>
            /// The Android.App.Notification.
            /// </returns>
            Notification UpdateNotification(Context context);

            #endregion
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets or sets PendingIntent.
        /// </summary>
        public PendingIntent PendingIntent { get; set; }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// The on download progress.
        /// </summary>
        /// <param name="progress">
        /// The progress.
        /// </param>
        public void OnDownloadProgress(DownloadProgressInfo progress)
        {
            this.progressInfo = progress;
            if (null != this.clientProxy)
            {
                this.clientProxy.OnDownloadProgress(progress);
            }

            if (CustomNotificationFactory.Notification != null)
            {
                if (progress.OverallTotal <= 0)
                {
                    // we just show the text
                    this.notification.TickerText = new String(this.currentTitle);
                    this.notification.Icon = Resource.Drawable.StatSysDownload;
                    this.notification.SetLatestEventInfo(this.context, this.label, this.currentText, this.PendingIntent);
                    this.currentNotification = this.notification;
                }
                else
                {
                    CustomNotificationFactory.Notification.CurrentBytes = progress.OverallProgress;
                    CustomNotificationFactory.Notification.TotalBytes = progress.OverallTotal;
                    CustomNotificationFactory.Notification.Icon = Resource.Drawable.StatSysDownload;
                    CustomNotificationFactory.Notification.PendingIntent = this.PendingIntent;
                    CustomNotificationFactory.Notification.Ticker = this.label + ": " + this.currentText;
                    CustomNotificationFactory.Notification.Title = this.label;
                    CustomNotificationFactory.Notification.TimeRemaining = progress.TimeRemaining;
                    this.currentNotification = CustomNotificationFactory.Notification.UpdateNotification(this.context);
                }

                this.notificationManager.Notify(NotificationId, this.currentNotification);
            }
        }

        /// <summary>
        /// The on download state changed.
        /// </summary>
        /// <param name="newState">
        /// The new state.
        /// </param>
        public void OnDownloadStateChanged(DownloaderState newState)
        {
            if (null != this.clientProxy)
            {
                this.clientProxy.OnDownloadStateChanged(newState);
            }

            if (newState != this.clientState)
            {
                this.clientState = newState;
                if (newState == DownloaderState.Idle || null == this.PendingIntent)
                {
                    return;
                }

                string stringDownload;
                int iconResource;
                bool ongoingEvent;

                // get the new title string and paused text
                switch (newState)
                {
                    case DownloaderState.Downloading:
                        iconResource = Resource.Drawable.StatSysDownload;
                        stringDownload = Helpers.GetDownloaderStringFromState(newState);
                        ongoingEvent = true;
                        break;

                    case DownloaderState.FetchingUrl:
                    case DownloaderState.Connecting:
                        iconResource = Resource.Drawable.StatSysDownloadDone;
                        stringDownload = Helpers.GetDownloaderStringFromState(newState);
                        ongoingEvent = true;
                        break;

                    case DownloaderState.Completed:
                    case DownloaderState.PausedByRequest:
                        iconResource = Resource.Drawable.StatSysDownloadDone;
                        stringDownload = Helpers.GetDownloaderStringFromState(newState);
                        ongoingEvent = false;
                        break;

                    case DownloaderState.Failed:
                    case DownloaderState.FailedCanceled:
                    case DownloaderState.FailedFetchingUrl:
                    case DownloaderState.FailedSdCardFull:
                    case DownloaderState.FailedUnlicensed:
                        iconResource = Resource.Drawable.StatSysWarning;
                        stringDownload = Helpers.GetDownloaderStringFromState(newState);
                        ongoingEvent = false;
                        break;

                    default:
                        iconResource = Resource.Drawable.StatSysWarning;
                        stringDownload = Helpers.GetDownloaderStringFromState(newState);
                        ongoingEvent = true;
                        break;
                }

                this.currentText = stringDownload;
                this.currentTitle = this.label;

                if (CustomNotificationFactory.Notification != null)
                {
                    this.currentNotification.TickerText = new String(this.label + ": " + this.currentText);
                    this.currentNotification.Icon = iconResource;
                    this.currentNotification.SetLatestEventInfo(this.context, this.currentTitle, this.currentText, this.PendingIntent);
                    if (ongoingEvent)
                    {
                        this.currentNotification.Flags |= NotificationFlags.OngoingEvent;
                    }
                    else
                    {
                        this.currentNotification.Flags &= ~NotificationFlags.OngoingEvent;
                        this.currentNotification.Flags |= NotificationFlags.AutoCancel;
                    }

                    this.notificationManager.Notify(NotificationId, this.currentNotification);
                }
            }
        }

        /// <summary>
        /// The on service connected.
        /// </summary>
        /// <param name="m">
        /// The m.
        /// </param>
        public void OnServiceConnected(Messenger m)
        {
        }

        /// <summary>
        /// The resend state.
        /// </summary>
        public void ResendState()
        {
            if (null != this.clientProxy)
            {
                this.clientProxy.OnDownloadStateChanged(this.clientState);
            }
        }

        /// <summary>
        /// Called in response to OnClientUpdated. Creates a new proxy and 
        /// notifies it of the current state.
        /// </summary>
        /// <param name="msg">
        /// the client Messenger to notify
        /// </param>
        public void SetMessenger(Messenger msg)
        {
            this.clientProxy = ClientMarshaller.CreateProxy(msg);
            if (null != this.progressInfo)
            {
                this.clientProxy.OnDownloadProgress(this.progressInfo);
            }

            if (this.clientState != DownloaderState.Unknown)
            {
                this.clientProxy.OnDownloadStateChanged(this.clientState);
            }
        }

        #endregion
    }
}