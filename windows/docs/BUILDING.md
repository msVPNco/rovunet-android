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
