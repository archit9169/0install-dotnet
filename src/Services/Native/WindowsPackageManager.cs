// Copyright Bastian Eicher et al.
// Licensed under the GNU Lesser Public License

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NanoByte.Common;
using NanoByte.Common.Collections;
using NanoByte.Common.Native;
using ZeroInstall.Model;

namespace ZeroInstall.Services.Native
{
    /// <summary>
    /// Detects common Windows software packages (such as Java and .NET) that are installed natively.
    /// </summary>
    public class WindowsPackageManager : PackageManagerBase
    {
        public WindowsPackageManager()
        {
            if (!WindowsUtils.IsWindows) throw new NotSupportedException("Windows Package Manager can only be used on the Windows platform.");
        }

        /// <inheritdoc/>
        protected override string DistributionName => "Windows";

        /// <inheritdoc/>
        protected override IEnumerable<ExternalImplementation> GetImplementations(string packageName)
            => (packageName ?? throw new ArgumentNullException(nameof(packageName))) switch
            {
                "openjdk-6-jre" => FindJre(6),
                "openjdk-7-jre" => FindJre(7),
                "openjdk-8-jre" => FindJre(8),
                "openjdk-9-jre" => FindJre(9),
                "openjdk-10-jre" => FindJre(10),
                "openjdk-6-jdk" => FindJdk(6),
                "openjdk-7-jdk" => FindJdk(7),
                "openjdk-8-jdk" => FindJdk(8),
                "openjdk-9-jdk" => FindJdk(9),
                "openjdk-10-jdk" => FindJdk(10),
                "netfx" => new[]
                {
                    // See: https://docs.microsoft.com/en-us/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed
                    FindNetFx("2.0", WindowsUtils.NetFx20, WindowsUtils.NetFx20),
                    FindNetFx("3.0", WindowsUtils.NetFx20, WindowsUtils.NetFx30),
                    FindNetFx("3.5", WindowsUtils.NetFx20, WindowsUtils.NetFx35),
                    FindNetFx("4.0", WindowsUtils.NetFx40, @"v4\Full"),
                    FindNetFx("4.5", WindowsUtils.NetFx40, @"v4\Full", 378389),
                    FindNetFx("4.5.1", WindowsUtils.NetFx40, @"v4\Full", 378675), // also covers 378758
                    FindNetFx("4.5.2", WindowsUtils.NetFx40, @"v4\Full", 379893),
                    FindNetFx("4.6", WindowsUtils.NetFx40, @"v4\Full", 393295), // also covers 393297
                    FindNetFx("4.6.1", WindowsUtils.NetFx40, @"v4\Full", 394254),
                    FindNetFx("4.6.2", WindowsUtils.NetFx40, @"v4\Full", 394802), // also covers 394806
                    FindNetFx("4.7", WindowsUtils.NetFx40, @"v4\Full", 460798), // also covers 460805
                    FindNetFx("4.7.1", WindowsUtils.NetFx40, @"v4\Full", 461308), // also covers 461310
                    FindNetFx("4.7.2", WindowsUtils.NetFx40, @"v4\Full", 461808), // also covers 461814
                    FindNetFx("4.8", WindowsUtils.NetFx40, @"v4\Full", 528040) // also covers 528049
                }.Flatten(),
                "netfx-client" => FindNetFx("4.0", WindowsUtils.NetFx40, @"v4\Client"),
                "powershell" => FindPowerShell(),
                "git" => FindGitForWindows(),
                _ => Enumerable.Empty<ExternalImplementation>()
            };

        private IEnumerable<ExternalImplementation> FindJre(int version) => FindJava(version,
            typeShort: "jre",
            typeLong: "Java Runtime Environment",
            mainExe: "java",
            secondaryCommand: Command.NameRunGui,
            secondaryExe: "javaw");

        private IEnumerable<ExternalImplementation> FindJdk(int version) => FindJava(version,
            typeShort: "jdk",
            typeLong: "Java Development Kit",
            mainExe: "javac",
            secondaryCommand: "java",
            secondaryExe: "java");

        private IEnumerable<ExternalImplementation> FindJava(int version, string typeShort, string typeLong, string mainExe, string secondaryCommand, string secondaryExe)
            => from javaHome in GetRegisteredPaths(@"JavaSoft\" + typeLong + @"\1." + version, "JavaHome")
               let mainPath = Path.Combine(javaHome.Value, @"bin\" + mainExe + ".exe")
               let secondaryPath = Path.Combine(javaHome.Value, @"bin\" + secondaryExe + ".exe")
               where File.Exists(mainPath) && File.Exists(secondaryPath)
               select new ExternalImplementation(DistributionName,
                   package: "openjdk-" + version + "-" + typeShort,
                   version: new ImplementationVersion(FileVersionInfo.GetVersionInfo(mainPath).ProductVersion.GetLeftPartAtLastOccurrence(".")), // Trim patch level
                   cpu: javaHome.Key)
               {
                   Commands =
                   {
                       new Command {Name = Command.NameRun, Path = mainPath},
                       new Command {Name = secondaryCommand, Path = secondaryPath}
                   },
                   IsInstalled = true,
                   QuickTestFile = mainPath
               };

        private static IEnumerable<KeyValuePair<Cpu, string>> GetRegisteredPaths(string registrySuffix, string valueName)
        {
            // Check for system native architecture (may be 32-bit or 64-bit)
            string? path = RegistryUtils.GetString(@"HKEY_LOCAL_MACHINE\SOFTWARE\" + registrySuffix, valueName);
            if (!string.IsNullOrEmpty(path))
                yield return new KeyValuePair<Cpu, string>(Architecture.CurrentSystem.Cpu, path);

            // Check for 32-bit on a 64-bit system
            if (OSUtils.Is64BitProcess)
            {
                path = RegistryUtils.GetString(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\" + registrySuffix, valueName);
                if (!string.IsNullOrEmpty(path))
                    yield return new KeyValuePair<Cpu, string>(Cpu.I486, path);
            }
        }

        // Uses detection logic described here: http://msdn.microsoft.com/library/hh925568
        private IEnumerable<ExternalImplementation> FindNetFx(string version, string clrVersion, string registryVersion, int releaseNumber = 0)
        {
            ExternalImplementation Impl(Cpu cpu) => new ExternalImplementation(DistributionName, "netfx", new ImplementationVersion(version), cpu)
            {
                // .NET executables do not need a runner on Windows
                Commands = {new Command {Name = Command.NameRun, Path = ""}},
                IsInstalled = true,
                QuickTestFile = Path.Combine(WindowsUtils.GetNetFxDirectory(clrVersion), "mscorlib.dll")
            };

            // Check for system native architecture (may be 32-bit or 64-bit)
            int install = RegistryUtils.GetDword(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\" + registryVersion, "Install");
            int release = RegistryUtils.GetDword(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\" + registryVersion, "Release");
            if (install == 1 && release >= releaseNumber)
                yield return Impl(Architecture.CurrentSystem.Cpu);

            // Check for 32-bit on a 64-bit system
            if (OSUtils.Is64BitProcess)
            {
                install = RegistryUtils.GetDword(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\NET Framework Setup\NDP\" + registryVersion, "Install");
                release = RegistryUtils.GetDword(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\NET Framework Setup\NDP\" + registryVersion, "Release");
                if (install == 1 && release >= releaseNumber)
                    yield return Impl(Cpu.I486);
            }
        }

        private IEnumerable<ExternalImplementation> FindPowerShell()
        {
            ExternalImplementation? Impl(string baseVersion, bool wow6432)
            {
                string regPrefix = $@"HKEY_LOCAL_MACHINE\SOFTWARE\{(wow6432 ? @"Wow6432Node\" : "")}Microsoft\PowerShell\{baseVersion}";
                if (RegistryUtils.GetDword(regPrefix, "Install") != 1) return null;

                string? version = RegistryUtils.GetString($@"{regPrefix}\PowerShellEngine", "PowerShellVersion");
                if (string.IsNullOrEmpty(version)) return null;

                return new ExternalImplementation(DistributionName, "powershell",
                    version: new ImplementationVersion(version),
                    cpu: wow6432 ? Cpu.I486 : Architecture.CurrentSystem.Cpu)
                {
                    Commands =
                    {
                        new Command
                        {
                            Name = Command.NameRun,
                            Path = Path.Combine(RegistryUtils.GetString($@"{regPrefix}\PowerShellEngine", "ApplicationBase"), "powershell.exe")
                        }
                    },
                    IsInstalled = true
                };
            }

            var impl = Impl(baseVersion: "3", wow6432: false) ?? Impl(baseVersion: "1", wow6432: false);
            if (impl != null) yield return impl;

            if (OSUtils.Is64BitProcess)
            {
                impl = Impl(baseVersion: "3", wow6432: true) ?? Impl(baseVersion: "1", wow6432: true);
                if (impl != null) yield return impl;
            }
        }

        private IEnumerable<ExternalImplementation> FindGitForWindows()
        {
            ExternalImplementation? Impl(bool wow6432)
            {
                string regKey = $@"HKEY_LOCAL_MACHINE\SOFTWARE\{(wow6432 ? @"Wow6432Node\" : "")}GitForWindows";
                string? version = RegistryUtils.GetString(regKey, "CurrentVersion");
                string? path = RegistryUtils.GetString(regKey, "InstallPath");
                if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(path)) return null;

                return new ExternalImplementation(DistributionName, "git",
                    version: new ImplementationVersion(version),
                    cpu: wow6432 ? Cpu.I486 : Architecture.CurrentSystem.Cpu)
                {
                    Commands =
                    {
                        new Command {Name = Command.NameRun, Path = Path.Combine(path, @"cmd\git.exe")},
                        new Command {Name = Command.NameRunGui, Path = Path.Combine(path, @"cmd\git-gui.exe")},
                        new Command {Name = "gitk", Path = Path.Combine(path, @"cmd\gitk.exe")},
                        new Command {Name = "start-ssh-agent", Path = Path.Combine(path, @"cmd\start-ssh-agent.exe")},
                        new Command {Name = "git-bash", Path = Path.Combine(path, @"git-bash.exe")},
                        new Command {Name = "git-cmd", Path = Path.Combine(path, @"git-cmd.exe")},
                        new Command {Name = "bash", Path = Path.Combine(path, @"usr\bin\bash.exe")},
                        new Command {Name = "sh", Path = Path.Combine(path, @"usr\bin\sh.exe")},
                        new Command {Name = "ssh", Path = Path.Combine(path, @"usr\bin\ssh.exe")},
                        new Command {Name = "scp", Path = Path.Combine(path, @"usr\bin\scp.exe")},
                        new Command {Name = "gpg", Path = Path.Combine(path, @"usr\bin\gpg.exe")},
                        new Command {Name = "gpgv", Path = Path.Combine(path, @"usr\bin\gpgv.exe")},
                        new Command {Name = "gpgsplit", Path = Path.Combine(path, @"usr\bin\gpgsplit.exe")}
                    },
                    IsInstalled = true
                };
            }

            var impl = Impl(wow6432: false);
            if (impl != null) yield return impl;

            if (OSUtils.Is64BitProcess)
            {
                impl = Impl(wow6432: true);
                if (impl != null) yield return impl;
            }
        }
    }
}
