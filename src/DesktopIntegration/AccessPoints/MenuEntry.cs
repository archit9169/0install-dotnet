// Copyright Bastian Eicher et al.
// Licensed under the GNU Lesser Public License

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using NanoByte.Common.Native;
using ZeroInstall.Model;
using ZeroInstall.Store;

namespace ZeroInstall.DesktopIntegration.AccessPoints
{
    /// <summary>
    /// Creates an entry for an application in the user's application menu (i.e. Windows start menu, GNOME application menu, etc.).
    /// </summary>
    [XmlType("menu-entry", Namespace = AppList.XmlNamespace)]
    public class MenuEntry : IconAccessPoint, IEquatable<MenuEntry>
    {
        #region Constants
        /// <summary>
        /// The name of this category of <see cref="AccessPoint"/>s as used by command-line interfaces.
        /// </summary>
        public const string CategoryName = "menu";
        #endregion

        /// <inheritdoc/>
        public override IEnumerable<string> GetConflictIDs(AppEntry appEntry) => new[] {$@"menu:{Category}\{Name}"};

        /// <summary>
        /// The category or folder in the menu to add the entry to. Leave empty for top-level entry.
        /// </summary>
        [Description("The category or folder in the menu to add the entry to. Leave empty for top-level entry.")]
        [XmlAttribute("category")]
        public string? Category { get; set; }

        /// <inheritdoc/>
        public override void Apply(AppEntry appEntry, Feed feed, IIconStore iconStore, bool machineWide)
        {
            #region Sanity checks
            if (appEntry == null) throw new ArgumentNullException(nameof(appEntry));
            if (iconStore == null) throw new ArgumentNullException(nameof(iconStore));
            #endregion

            var target = new FeedTarget(appEntry.InterfaceUri, feed);
            if (WindowsUtils.IsWindows) Windows.Shortcut.Create(this, target, iconStore, machineWide);
            else if (UnixUtils.IsUnix) Unix.FreeDesktop.Create(this, target, iconStore, machineWide);
        }

        /// <inheritdoc/>
        public override void Unapply(AppEntry appEntry, bool machineWide)
        {
            #region Sanity checks
            if (appEntry == null) throw new ArgumentNullException(nameof(appEntry));
            #endregion

            if (WindowsUtils.IsWindows) Windows.Shortcut.Remove(this, machineWide);
            else if (UnixUtils.IsUnix) Unix.FreeDesktop.Remove(this, machineWide);
        }

        #region Clone
        /// <inheritdoc/>
        public override AccessPoint Clone() => new MenuEntry {UnknownAttributes = UnknownAttributes, UnknownElements = UnknownElements, Name = Name, Command = Command, Category = Category};
        #endregion

        #region Equality
        /// <inheritdoc/>
        public bool Equals(MenuEntry other)
            => other != null && (base.Equals(other) && other.Category == Category);

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (obj == null) return false;
            if (obj == this) return true;
            return obj.GetType() == typeof(MenuEntry) && Equals((MenuEntry)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
            => HashCode.Combine(base.GetHashCode(), Category);
        #endregion
    }
}
