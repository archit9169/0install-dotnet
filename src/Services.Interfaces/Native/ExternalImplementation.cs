// Copyright Bastian Eicher et al.
// Licensed under the GNU Lesser Public License

using System;
using NanoByte.Common.Values;
using ZeroInstall.Model;
using ZeroInstall.Model.Selection;

namespace ZeroInstall.Services.Native
{
    /// <summary>
    /// An implementation provided by an external package manager.
    /// </summary>
    /// <seealso cref="IPackageManager"/>
    public sealed class ExternalImplementation : Implementation, IEquatable<ExternalImplementation>
    {
        #region Constants
        /// <summary>
        /// This is prepended to <see cref="ImplementationBase.ID"/> for all <see cref="ExternalImplementation"/>.
        /// </summary>
        /// <remarks>Also used to mark regular <see cref="Implementation"/>s that act as proxies for <see cref="ExternalImplementation"/>s.</remarks>
        public const string PackagePrefix = "package:";
        #endregion

        /// <summary>
        /// The name of the distribution (e.g. Debian, RPM) where this implementation comes from.
        /// </summary>
        public string Distribution { get; set; }

        /// <summary>
        /// The name of the package in the <see cref="Distribution"/>.
        /// </summary>
        public string Package { get; set; }

        /// <summary>
        /// Indicates whether this implementation is currently installed.
        /// </summary>
        public bool IsInstalled { get; set; }

        /// <summary>
        /// A file which, if present, indicates that this implementation <see cref="IsInstalled"/>.
        /// </summary>
        /// <remarks>This makes it possible to avoid <see cref="IPackageManager.Lookup"/> calls for better performance.</remarks>
        /// <seealso cref="ImplementationSelection.QuickTestFile"/>
        public string? QuickTestFile { get; set; }

        /// <summary>
        /// Creates a new external implementation.
        /// </summary>
        /// <param name="distribution">The name of the distribution (e.g. Debian, RPM) where this implementation comes from.</param>
        /// <param name="package">The name of the package in the <paramref name="distribution"/>.</param>
        /// <param name="version">The version number of the implementation.</param>
        /// <param name="cpu">For platform-specific binaries, the CPU architecture for which the implementation was compiled.</param>
        public ExternalImplementation(string distribution, string package, ImplementationVersion version, Cpu cpu = Cpu.All)
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(distribution)) throw new ArgumentNullException(nameof(distribution));
            if (string.IsNullOrEmpty(package)) throw new ArgumentNullException(nameof(package));
            if (version == null) throw new ArgumentNullException(nameof(version));
            #endregion

            ID = PackagePrefix + distribution.ToLowerInvariant() + ":" + package + ":" + version;

            Version = version;
            Stability = Stability.Packaged;
            Distribution = distribution;
            Package = package;

            if (cpu != Cpu.All)
            {
                ID += ":" + cpu.ConvertToString();
                Architecture = new Architecture(OS.All, cpu);
            }
        }

        /// <summary>
        /// Creates a new external implementation from an <see cref="ImplementationBase.ID"/>.
        /// </summary>
        /// <param name="id">The ID to parse.</param>
        /// <exception cref="FormatException"><paramref name="id"/> is not a standard <see cref="ExternalImplementation"/> ID.</exception>
        public static ExternalImplementation FromID(string id)
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            #endregion

            var parts = id.Split(':');
            if (parts.Length < 4 || parts[0] + ":" != PackagePrefix)
                throw new FormatException();

            var implementation = new ExternalImplementation(distribution: parts[1], package: parts[2], version: new ImplementationVersion(parts[3])) {ID = id};
            if (parts.Length >= 5) implementation.Architecture = new Architecture(OS.All, parts[4].ConvertFromString<Cpu>());

            return implementation;
        }

        #region Equality
        /// <inheritdoc/>
        public bool Equals(ExternalImplementation other)
            => other != null
            && base.Equals(other)
            && Distribution == other.Distribution
            && Package == other.Package
            && IsInstalled == other.IsInstalled
            && QuickTestFile == other.QuickTestFile;

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (obj == null) return false;
            if (obj == this) return true;
            if (obj.GetType() != typeof(ExternalImplementation)) return false;
            return Equals((ExternalImplementation)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
            => HashCode.Combine(
                base.GetHashCode(),
                Distribution,
                Package,
                QuickTestFile);
        #endregion
    }
}
