// Copyright Bastian Eicher et al.
// Licensed under the GNU Lesser Public License

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using NanoByte.Common;
using NanoByte.Common.Collections;

namespace ZeroInstall.Model.Capabilities
{
    /// <summary>
    /// Groups a number of application <see cref="Capability"/>s (for a specific operating system) that can be registered in a desktop environment.
    /// </summary>
    [Description("Groups a number of application capabilities (for a specific operating system) that can be registered in a desktop environment.")]
    [Serializable, XmlRoot("capabilities", Namespace = XmlNamespace), XmlType("capabilities", Namespace = XmlNamespace)]
    public sealed class CapabilityList : XmlUnknown, ICloneable<CapabilityList>, IEquatable<CapabilityList>
    {
        #region Constants
        /// <summary>
        /// The XML namespace used for storing application capabilities.
        /// </summary>
        public const string XmlNamespace = "http://0install.de/schema/desktop-integration/capabilities";

        /// <summary>
        /// The URI to retrieve an XSD containing the XML Schema information for this class in serialized form.
        /// </summary>
        public const string XsdLocation = XmlNamespace + "/capabilities.xsd";
        #endregion

        /// <summary>
        /// Determines for which operating system the <see cref="Capability"/>s are applicable.
        /// </summary>
        [Description("Determines for which operating system the capabilities are applicable.")]
        [XmlAttribute("os"), DefaultValue(typeof(OS), "All")]
        public OS OS { get; set; }

        /// <summary>
        /// A list of <see cref="Capability"/>s.
        /// </summary>
        [Browsable(false)]
        [XmlElement(typeof(AppRegistration)), XmlElement(typeof(AutoPlay)), XmlElement(typeof(ComServer)), XmlElement(typeof(ContextMenu)), XmlElement(typeof(DefaultProgram)), XmlElement(typeof(FileType)), XmlElement(typeof(UrlProtocol))]
        public List<Capability> Entries { get; } = new List<Capability>();

        /// <summary>
        /// Retrieves the first <see cref="Capability"/> that matches a specific type and ID. Safe for missing elements.
        /// </summary>
        /// <typeparam name="T">The capability type to match.</typeparam>
        /// <param name="id">The <see cref="Capability.ID"/> to match.</param>
        /// <returns>The first matching <see cref="Capability"/>; <c>null</c> if no match was found.</returns>
        /// <exception cref="KeyNotFoundException">No capability matching <paramref name="id"/> and <typeparamref name="T"/> was found.</exception>
        public T? GetCapability<T>(string id) where T : Capability
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            #endregion

            return Entries.OfType<T>().FirstOrDefault(specificCapability => specificCapability.ID == id);
        }

        #region Clone
        /// <summary>
        /// Creates a deep copy of this <see cref="CapabilityList"/> instance.
        /// </summary>
        /// <returns>The new copy of the <see cref="CapabilityList"/>.</returns>
        public CapabilityList Clone()
        {
            var capabilityList = new CapabilityList {UnknownAttributes = UnknownAttributes, UnknownElements = UnknownElements, OS = OS};
            capabilityList.Entries.AddRange(Entries.CloneElements());
            return capabilityList;
        }
        #endregion

        #region Conversion
        /// <summary>
        /// Returns the capability list in the form "OS". Not safe for parsing!
        /// </summary>
        public override string ToString() => OS.ToString();
        #endregion

        #region Equality
        /// <inheritdoc/>
        public bool Equals(CapabilityList? other)
            => other != null
            && base.Equals(other)
            && OS == other.OS
            && Entries.SequencedEquals(other.Entries);

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (obj == null) return false;
            if (obj == this) return true;
            return obj is CapabilityList list && Equals(list);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
            => HashCode.Combine(
                base.GetHashCode(),
                OS,
                Entries.GetSequencedHashCode());
        #endregion
    }
}
