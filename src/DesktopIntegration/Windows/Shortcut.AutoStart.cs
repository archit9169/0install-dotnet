// Copyright Bastian Eicher et al.
// Licensed under the GNU Lesser Public License

using System;
using System.IO;
using NanoByte.Common;
using ZeroInstall.DesktopIntegration.AccessPoints;
using ZeroInstall.Model;
using ZeroInstall.Store;

namespace ZeroInstall.DesktopIntegration.Windows
{
    public static partial class Shortcut
    {
        /// <summary>
        /// Creates a new Windows shortcut in the "Startup" menu.
        /// </summary>
        /// <param name="autoStart">Information about the shortcut to be created.</param>
        /// <param name="target">The target the shortcut shall point to.</param>
        /// <param name="iconStore">Stores icon files downloaded from the web as local files.</param>
        /// <param name="machineWide">Create the shortcut machine-wide instead of just for the current user.</param>
        public static void Create(AutoStart autoStart, FeedTarget target, IIconStore iconStore, bool machineWide)
        {
            #region Sanity checks
            if (autoStart == null) throw new ArgumentNullException(nameof(autoStart));
            if (iconStore == null) throw new ArgumentNullException(nameof(iconStore));
            #endregion

            string filePath = GetStartupPath(autoStart.Name, machineWide);
            Create(filePath, targetPath: StubBuilder.GetRunStub(target, autoStart.Command, iconStore));
        }

        /// <summary>
        /// Removes a Windows shortcut from the "Startup" menu.
        /// </summary>
        /// <param name="autoStart">Information about the shortcut to be removed.</param>
        /// <param name="machineWide">The shortcut was created machine-wide instead of just for the current user.</param>
        public static void Remove(AutoStart autoStart, bool machineWide)
        {
            #region Sanity checks
            if (autoStart == null) throw new ArgumentNullException(nameof(autoStart));
            #endregion

            string filePath = GetStartupPath(autoStart.Name, machineWide);
            if (File.Exists(filePath)) File.Delete(filePath);
        }

        private static string GetStartupPath(string name, bool machineWide)
        {
            CheckName(name);

            string startupDir = machineWide
                ? RegistryUtils.GetString(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders", "Common Startup")
                : Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            return Path.Combine(startupDir, name + ".lnk");
        }
    }
}
