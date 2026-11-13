# Preview.6 build provenance

Use a fresh output directory and an explicit current Git SourceRevision. The six native engines are reused unchanged from the verified preview.5/review4 payload with their existing pins; they are not newly rebuilt by this packaging run. Source/normal payload/experimental lab must share the same revision and Core/Platform assemblies. Windows code signing and public publication remain separate from local packaging.

The optional scripts/build-network-lab.ps1 creates a separate VM-gated package, verifies the official Wintun signature and requires the matching normal payload. It does not install/elevate or run native networking on the build host. Its build metadata describes implemented capabilities; a separate exact-byte native acceptance report is required. Never include private VM controller files, credentials, keys or disks in corresponding source/installer assets.

Historical build notes follow.
# Build Rovunet Windows 0.1.1-preview.1 (local UI review)

Use Windows x64, .NET SDK10.0.403/runtime10.0.14, Go1.27.3 and NSIS3.09.
The desktop runtime is bundled; end users do not need these build tools.
Do not use Android signing keys. Windows binaries in this local review are unsigned.

From windows/:

```powershell
dotnet build tests/Rovunet.Tests -c Release -m:1
dotnet tests/Rovunet.Tests/bin/Release/net10.0/Rovunet.Tests.dll
$env:CGO_ENABLED='0'
go -C transports test -mod=vendor -count=1 .
go -C transports build -mod=vendor -buildvcs=false -trimpath -ldflags='-s -w' -o ../engines/windows-x64/rovunet-transports.exe .
```

Obtain the separately executed official Xray, NaiveProxy and Slipstream binaries
using scripts/fetch-engines.py, fetch-naive.py and fetch-slipstream.py. Python3
is required for these download helpers. Verify the exact hashes in
engines/pins.json, engines/transports-pins.json and scripts/package-windows.ps1.
Native build reproducibility can depend on the exact compiler/environment;
do not silently change the expected pin after an unexplained mismatch.

Build the self-contained installer and explicit corresponding-source archive:

```powershell
$revision=(git rev-parse HEAD).Trim()
./scripts/package-windows.ps1 -Dotnet C:/tools/dotnet/dotnet.exe -MakeNsis C:/tools/nsis/makensis.exe -EngineDirectory ./engines/windows-x64 -OutputDirectory C:/build/Rovunet-ui-review -SourceRevision $revision
```

Use fresh absolute output paths. The pipeline restores lock files before its
source snapshot, then publishes without restore and verifies source immutability.
It packages the four pinned engines, six DNS banks, legal notices and public
documentation. The source manifest excludes bin/obj/artifacts and native EXEs,
but retains engine pins. The supplied SourceRevision must identify this source
commit; SOURCE-COMMIT.txt records the historical imported baseline, not the
current Git HEAD. Publish notes must distinguish package-source and publication
documentation commits if they differ.

The original review scripts are retained as historical development tools.
package-windows.ps1 is the packaging entry point; it reads the version from Directory.Build.props and passes it to both source archive and installer registration. Packaging does not upload
or publish anything by itself. Run regression fixtures with synthetic data and
isolated installation identities; never replace a user's normal installation
or expose real profile input in command-line arguments or public reports.

The four native engines are unchanged from the 0.1.0 Beta payload. For this UI-only review, reuse those exact pinned files rather than recompiling them. Restore using the exact publish properties before committing the source and obtain a real local Git revision; never label changed source with the old Beta commit. Local review commits need no remote and are not publication. The source archive is generated before publish, then checked for mutations.

Stage25 supplemental engine: native/extra/BUILDING.md. Tor expert bundle files and manifest are required; the package script validates all pins before inclusion. The VayDNS client fork adds an optional TCP chain dialer while retaining TLS validation. Android/macOS references were read-only.
