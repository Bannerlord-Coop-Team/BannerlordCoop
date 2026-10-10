Voice dependencies
==================

Concentus 1.1.7 (managed Opus encoder/decoder)
  Package: https://www.nuget.org/packages/Concentus/1.1.7
  Package license declaration: https://opus-codec.org/license/
  Copyright and BSD terms: Concentus-LICENSE.txt
  Source: https://github.com/lostromb/concentus
  The included copyright notice is from upstream commit
  4b1217eadd9e434dc7ffac3e070210e6f04e3e7f (July 14, 2017).
  The package does not contain a versioned LICENSE file; its nuspec explicitly
  declares the Opus license URL. That license permits binary redistribution
  with the included notice. The package contains managed netstandard1.0 code,
  not the native Opus binaries mentioned by newer Concentus releases.
  Opus patent grants and their conditions: https://opus-codec.org/license/#patents

NAudio.WinMM 2.2.1 and NAudio.Core 2.2.1
  Package: https://www.nuget.org/packages/NAudio.WinMM/2.2.1
  Source/license: https://github.com/naudio/NAudio/tree/v2.2.1
  MIT terms: NAudio-LICENSE.txt
  These are managed libraries using the operating system's Windows audio APIs.

Microsoft.Win32.Registry, System.Security.AccessControl,
System.Security.Principal.Windows 4.7.0 (NAudio transitive dependencies)
  MIT terms: Microsoft-NET-LICENSE.txt
  Additional notices: Microsoft-NET-THIRD-PARTY-NOTICES.txt
  These two files are byte-identical across all three NuGet packages.

Keep this entire ThirdPartyNotices directory with redistributed module builds.
These dependencies retain their own licenses; they are not relicensed under
BannerlordCoop's source-available license. No external voice service is used.
