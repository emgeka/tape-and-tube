# Digital host C ABI smoke consumer

GPL-3.0-only. This standalone .NET 10 console consumer has no WPF or NuGet package dependencies. Its native declarations follow `../digital-host/include/tapetube_digital.h`, API 1. It loads the explicitly supplied library by absolute path and resolves its exports with `NativeLibrary`.

From the repository root on Windows:

```powershell
. ./scripts/environment.ps1
& $Dotnet build ./prototypes/processing-core/abi-smoke/AbiSmoke.csproj -c Release -o ./artifacts/core-prototype/abi-smoke
& $Dotnet ./artifacts/core-prototype/abi-smoke/TapeTube.DigitalAbiSmoke.dll (Join-Path $ProjectRoot 'artifacts/core-prototype/target/release/tapetube_digital_host.dll')
```

On a prepared 64-bit Linux system with .NET 10 and the matching native host build:

```sh
dotnet build prototypes/processing-core/abi-smoke/AbiSmoke.csproj -c Release -o artifacts/core-prototype/abi-smoke
dotnet artifacts/core-prototype/abi-smoke/TapeTube.DigitalAbiSmoke.dll "$(pwd)/artifacts/core-prototype/target/release/libtapetube_digital_host.so"
```

These Linux commands describe how to run the same consumer; they do not establish that a Linux run has occurred.

The consumer reports a single JSON result on stdout and exits nonzero on failure. It exercises metadata/default strings and ownership, independent concurrent session handles, DCT and Video 1, prediction/datamosh/data loss, genuine earlier source-frame callbacks, exact sequential/seek/loop/skipped-display comparisons, reset, stride padding and allocation-tail sentinels, callback failure/recovery, invalid descriptors, and stale/double-destroy errors. Synthetic input is RGBA8, 64×48, 12 fps, 42 frames. Expected callback failures may also produce native panic-hook diagnostics on stderr; the C boundary must still return an owned error and recover its session.

This is a CPU ABI/integration check. It does not test CRT rendering, FFmpeg, sound synchronization, a GUI or a native Linux desktop.
