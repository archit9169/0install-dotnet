// Copyright Bastian Eicher et al.
// Licensed under the GNU Lesser Public License

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using NanoByte.Common;
using NanoByte.Common.Net;
using NanoByte.Common.Storage;
using NanoByte.Common.Tasks;
using ZeroInstall.Model;
using ZeroInstall.Services.Native;
using ZeroInstall.Services.Properties;
using ZeroInstall.Store;
using ZeroInstall.Store.Implementations;

namespace ZeroInstall.Services.Fetchers
{
    /// <summary>
    /// Downloads <see cref="Implementation"/>s sequentially.
    /// </summary>
    public class SequentialFetcher : FetcherBase
    {
        #region Dependencies
        private readonly Config _config;

        /// <summary>
        /// Creates a new sequential download fetcher.
        /// </summary>
        /// <param name="config">User settings controlling network behaviour, solving, etc.</param>
        /// <param name="implementationStore">The location to store the downloaded and unpacked <see cref="Implementation"/>s in.</param>
        /// <param name="handler">A callback object used when the the user needs to be informed about progress.</param>
        public SequentialFetcher(Config config, IImplementationStore implementationStore, ITaskHandler handler)
            : base(implementationStore, handler)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }
        #endregion

        /// <inheritdoc/>
        public override void Fetch(IEnumerable<Implementation> implementations)
        {
            #region Sanity checks
            if (implementations == null) throw new ArgumentNullException(nameof(implementations));
            #endregion

            foreach (var implementation in implementations)
            {
                Handler.CancellationToken.ThrowIfCancellationRequested();
                Fetch(implementation, tag: implementation.ManifestDigest);
            }
        }

        /// <inheritdoc/>
        protected override string? Fetch(Implementation implementation, object tag)
        {
            #region Sanity checks
            if (implementation == null) throw new ArgumentNullException(nameof(implementation));
            #endregion

            // Use mutex to detect in-progress download of same implementation in other processes
            using var mutex = new Mutex(false, "0install-fetcher-" + GetDownloadID(implementation));
            try
            {
                while (!mutex.WaitOne(100, exitContext: false)) // NOTE: Might be blocked more than once
                    Handler.RunTask(new WaitTask(Resources.WaitingForDownload, mutex) {Tag = tag}); // Wait for mutex to be released
            }
            #region Error handling
            catch (AbandonedMutexException ex)
            {
                // Abandoned mutexes also get owned, but indicate something may have gone wrong elsewhere
                Log.Warn(ex.Message);
            }
            #endregion

            try
            {
                // Check if another process added the implementation in the meantime
                string? path = GetPathSafe(implementation);
                if (path != null) return path;

                if (implementation.RetrievalMethods.Count == 0) throw new NotSupportedException(string.Format(Resources.NoRetrievalMethod, implementation.ID));
                Retrieve(implementation);

                return GetPathSafe(implementation);
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Returns a unique identifier for an <see cref="Implementation"/>. Usually based on <see cref="ImplementationBase.ManifestDigest"/>.
        /// </summary>
        /// <exception cref="NotSupportedException"><paramref name="implementation"/> does not specify manifest digests in any known formats.</exception>
        private static string GetDownloadID(Implementation implementation)
        {
            if (implementation.ID.StartsWith(ExternalImplementation.PackagePrefix))
                return implementation.ID;
            else
            {
                string? digest = implementation.ManifestDigest.Best;
                if (digest == null) throw new NotSupportedException(string.Format(Resources.NoManifestDigest, implementation.ID));
                return digest;
            }
        }

        /// <inheritdoc/>
        protected override TemporaryFile Download(DownloadRetrievalMethod retrievalMethod, object? tag = null)
        {
            #region Sanity checks
            if (retrievalMethod == null) throw new ArgumentNullException(nameof(retrievalMethod));
            #endregion

            retrievalMethod.Validate();

            try
            {
                return base.Download(retrievalMethod, tag);
            }
            catch (WebException ex) when (!retrievalMethod.Href.IsLoopback && _config.FeedMirror != null)
            {
                Log.Warn(ex);
                Log.Info("Trying mirror");

                try
                {
                    var mirrored = (DownloadRetrievalMethod)retrievalMethod.Clone();
                    mirrored.Href = new Uri($"{_config.FeedMirror.EnsureTrailingSlash().AbsoluteUri}archive/{retrievalMethod.Href.Scheme}/{retrievalMethod.Href.Host}/{string.Concat(retrievalMethod.Href.Segments).TrimStart('/').Replace("/", "%23")}");
                    return base.Download(mirrored, tag);
                }
                catch (WebException)
                {
                    // Report the original problem instead of mirror errors
                    throw ex.PreserveStack();
                }
            }
        }
    }
}
