﻿/*
 * Copyright 2010-2016 Bastian Eicher
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser Public License for more details.
 *
 * You should have received a copy of the GNU Lesser Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;
using NanoByte.Common;
using NanoByte.Common.Net;

namespace ZeroInstall.Store.Model
{
    /// <summary>
    /// Represents a retrieval method that downloads data from the net.
    /// </summary>
    [XmlType("download-retrieval-method", Namespace = Feed.XmlNamespace)]
    public abstract class DownloadRetrievalMethod : RetrievalMethod, IRecipeStep
    {
        /// <summary>
        /// The URL to download the file from. Relative URLs are only allowed in local feed files.
        /// </summary>
        [Browsable(false)]
        [XmlIgnore]
        public Uri Href { get; set; }

        #region XML serialization
        /// <summary>Used for XML serialization and PropertyGrid.</summary>
        /// <seealso cref="Href"/>
        [DisplayName(@"Href"), Description("The URL to download the file from. Relative URLs are only allowed in local feed files.")]
        [XmlAttribute("href"), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), EditorBrowsable(EditorBrowsableState.Never)]
        public string HrefString { get => Href?.ToStringRfc(); set => Href = (string.IsNullOrEmpty(value) ? null : new Uri(value, UriKind.RelativeOrAbsolute)); }
        #endregion

        /// <summary>
        /// The size of the file in bytes. The file must have the given size or it will be rejected.
        /// </summary>
        [Description("The size of the file in bytes. The file must have the given size or it will be rejected.")]
        [XmlAttribute("size"), DefaultValue(0L)]
        public long Size { get; set; }

        /// <summary>
        /// The effective size of the file on the server.
        /// </summary>
        [XmlIgnore, Browsable(false)]
        public virtual long DownloadSize => Size;

        #region Normalize
        /// <inheritdoc cref="RetrievalMethod.Normalize"/>
        public override void Normalize(FeedUri feedUri)
        {
            #region Sanity checks
            if (feedUri == null) throw new ArgumentNullException(nameof(feedUri));
            #endregion

            base.Normalize(feedUri);

            if (Href != null) Href = ModelUtils.GetAbsoluteHref(Href, feedUri);
        }

        protected abstract string XmlTagName { get; }

        /// <summary>
        /// Performs sanity checks.
        /// </summary>
        /// <exception cref="InvalidDataException">One or more required fields are not set.</exception>
        public void Validate() => EnsureNotNull(Href, xmlAttribute: "href", xmlTag: XmlTagName);
        #endregion

        #region Clone
        /// <inheritdoc/>
        IRecipeStep ICloneable<IRecipeStep>.Clone() => (IRecipeStep)Clone();

        #endregion

        #region Equality
        protected bool Equals(DownloadRetrievalMethod other) => other != null && base.Equals(other) && other.Href == Href && other.Size == Size;

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result * 397) ^ Href?.GetHashCode() ?? 0;
                result = (result * 397) ^ Size.GetHashCode();
                return result;
            }
        }
        #endregion
    }
}
