// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DownloadStatusExtensions.cs" company="Matthew Leibowitz">
//   Copyright (c) Matthew Leibowitz
//   This code is licensed under the Apache 2.0 License
//   http://www.apache.org/licenses/LICENSE-2.0.html
// </copyright>
// <summary>
//   Extension methods to help filter the <see cref="DownloadStatus" /> values.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ExpansionDownloader
{
    /// <summary>
    /// Extension methods to help filter the <see cref="DownloadStatus"/> values.
    /// </summary>
    public static class DownloadStatusExtensions
    {
        #region Public Methods and Operators

        /// <summary>
        /// Returns whether the status is a client error (i.e. 4xx).
        /// </summary>
        /// <param name="status">
        /// The status.
        /// </param>
        /// <returns>
        /// The is status client error.
        /// </returns>
        public static bool IsClientError(this DownloadStatus status)
        {
            return status >= DownloadStatus.ClientErrorMinimum && status <= DownloadStatus.ClientErrorMaximum;
        }

        /// <summary>
        /// Returns whether the download has completed (either with success or
        /// error).
        /// </summary>
        /// <param name="status">
        /// The status.
        /// </param>
        /// <returns>
        /// The is status completed.
        /// </returns>
        public static bool IsCompleted(this DownloadStatus status)
        {
            return status.IsSuccess() || status.IsError();
        }

        /// <summary>
        /// Returns whether the status is an error (i.e. 4xx or 5xx).
        /// </summary>
        /// <param name="status">
        /// The status.
        /// </param>
        /// <returns>
        /// The is status error.
        /// </returns>
        public static bool IsError(this DownloadStatus status)
        {
            return status >= DownloadStatus.AnyErrorMinimum && status <= DownloadStatus.AnyErrorMaximum;
        }

        /// <summary>
        /// Returns whether the status is informational (i.e. 1xx).
        /// </summary>
        /// <param name="status">
        /// The status.
        /// </param>
        /// <returns>
        /// The is status informational.
        /// </returns>
        public static bool IsInformational(this DownloadStatus status)
        {
            return status >= DownloadStatus.InformationalMinimum && status <= DownloadStatus.InformationalMaximum;
        }

        /// <summary>
        /// Returns whether the status is a redirect (i.e. 3xx).
        /// </summary>
        /// <param name="status">
        /// The status.
        /// </param>
        /// <returns>
        /// The status a redirect.
        /// </returns>
        public static bool IsRedirect(this DownloadStatus status)
        {
            return status >= DownloadStatus.RedirectMinimum && status <= DownloadStatus.RedirectMaximum;
        }

        /// <summary>
        /// Returns whether the status is a server error (i.e. 5xx).
        /// </summary>
        /// <param name="status">
        /// The status.
        /// </param>
        /// <returns>
        /// The is status server error.
        /// </returns>
        public static bool IsServerError(this DownloadStatus status)
        {
            return status >= DownloadStatus.ServerErrorMinimum && status <= DownloadStatus.ServerErrorMaximum;
        }

        /// <summary>
        /// Returns whether the status is a success (i.e. 2xx).
        /// </summary>
        /// <param name="status">
        /// The status.
        /// </param>
        /// <returns>
        /// The is status success.
        /// </returns>
        public static bool IsSuccess(this DownloadStatus status)
        {
            return status >= DownloadStatus.SuccessMinimum && status <= DownloadStatus.SuccessMaximum;
        }

        #endregion
    }
}