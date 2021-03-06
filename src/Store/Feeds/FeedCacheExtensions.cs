// Copyright Bastian Eicher et al.
// Licensed under the GNU Lesser Public License

using System;
using System.Collections.Generic;
using System.IO;
using NanoByte.Common;
using ZeroInstall.Model;

namespace ZeroInstall.Store.Feeds
{
    /// <summary>
    /// Provides extensions methods for <see cref="IFeedCache"/>.
    /// </summary>
    public static class FeedCacheExtensions
    {
        /// <summary>
        /// Loads all <see cref="Feed"/>s stored in <see cref="IFeedCache"/> into memory.
        /// </summary>
        /// <param name="cache">The <see cref="IFeedCache"/> to load <see cref="Feed"/>s from.</param>
        /// <returns>The parsed <see cref="Feed"/>s. Damaged files are logged and skipped.</returns>
        /// <exception cref="IOException">A problem occurred while reading from the cache.</exception>
        /// <exception cref="UnauthorizedAccessException">Read access to the cache is not permitted.</exception>
        public static IEnumerable<Feed> GetAll(this IFeedCache cache)
        {
            #region Sanity checks
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            #endregion

            var feeds = new List<Feed>();
            foreach (var feedUri in cache.ListAll())
            {
                try
                {
                    feeds.Add(cache.GetFeed(feedUri));
                }
                #region Error handling
                catch (KeyNotFoundException)
                {
                    // Feed file no longer exists
                }
                catch (InvalidDataException ex)
                {
                    Log.Error(ex);
                }
                #endregion
            }
            return feeds;
        }
    }
}
