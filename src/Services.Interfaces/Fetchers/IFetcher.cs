// Copyright Bastian Eicher et al.
// Licensed under the GNU Lesser Public License

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using ZeroInstall.Model;
using ZeroInstall.Store.Implementations;

namespace ZeroInstall.Services.Fetchers
{
    /// <summary>
    /// Downloads <see cref="Implementation"/>s, extracts them and adds them to an <see cref="IImplementationStore"/>.
    /// </summary>
    /// <remarks>This is an application of the strategy pattern.</remarks>
    public interface IFetcher
    {
        /// <summary>
        /// Downloads a set of <see cref="Implementation"/>s to the <see cref="Store"/> and returns once this process is complete.
        /// </summary>
        /// <param name="implementations">The <see cref="Implementation"/>s to be downloaded.</param>
        /// <exception cref="OperationCanceledException">A download or IO task was canceled from another thread.</exception>
        /// <exception cref="WebException">A file could not be downloaded from the internet.</exception>
        /// <exception cref="NotSupportedException">A file format, protocol, etc. is unknown or not supported.</exception>
        /// <exception cref="IOException">A downloaded file could not be written to the disk or extracted.</exception>
        /// <exception cref="UnauthorizedAccessException">Write access to <see cref="IImplementationStore"/> is not permitted.</exception>
        /// <exception cref="DigestMismatchException">An <see cref="Implementation"/>'s <see cref="Archive"/>s don't match the associated <see cref="ManifestDigest"/>.</exception>
        void Fetch(IEnumerable<Implementation> implementations);
    }
}
