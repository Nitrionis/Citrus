// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CustomNotificationFactory.cs" company="Matthew Leibowitz">
//   Copyright (c) Matthew Leibowitz
//   This code is licensed under the Apache 2.0 License
//   http://www.apache.org/licenses/LICENSE-2.0.html
// </copyright>
// <summary>
//   The custom notification factory.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ExpansionDownloader
{
    /// <summary>
    /// The custom notification factory.
    /// </summary>
    public static class CustomNotificationFactory
    {
        #region Public Properties

        /// <summary>
        /// Gets or sets Notification.
        /// </summary>
        public static DownloadNotification.ICustomNotification Notification { get; set; }

        #endregion
    }
}