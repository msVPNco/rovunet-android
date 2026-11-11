# Rovunet Windows — attribution and source availability

Copyright (C) 2026 Rovunet contributors. Windows adaptation started 2026-11-08.
Licensed under GNU AGPL version 3; see LICENSE. Provided without warranty.
Recipients may copy, modify and redistribute under that license.

The profile formats, account-status logic and protocol conversion are adapted
from Rovunet Android alpha16, a modified version of SlipNet 2.5.6 by Anonvector,
Copyright (C) 2026 Anonvector. Not affiliated with or endorsed by SlipNet.
The Windows shell and lifecycle components are new C# implementations.
The original notice is retained at docs/references/ANDROID-NOTICE.md.

Corresponding Windows source is delivered with the release source archive and belongs
under windows/ in https://github.com/msVPNco/rovunet-app. No public Windows
release is implied by that URL. The source package includes build/install/test
scripts and a patch against the recorded central-repository base.

Xray-core is an unmodified separately executed program under MPL-2.0.
Official binary: v26.10.31, commit a4a3dff5b20c2faae567b742c172c7b95e75c083.
Source: https://github.com/XTLS/Xray-core/tree/a4a3dff5b20c2faae567b742c172c7b95e75c083
Full source is included as Xray-core-source.zip with the review source package.
See engines/pins.json and licenses/Xray-MPL-2.0.txt. Windows executable SHA-256:
d5b558191cccb1e92a5772b68b8cab67717b26f634b0f15153215f65ae982a7e
The Android pin 63ca2e0c1d31b89f67700c33842431f541fedd25 is a different revision.

.NET 10.0.14 / WPF, unmodified redistributable runtime: Microsoft and .NET
contributors; MIT and third-party notices under licenses/DotNet-*.
NSIS 3.09 installer: NSIS contributors and other copyright holders; see
licenses/NSIS-COPYRIGHT.txt. Individual components retain their own licenses.

Preview.2 includes a new AGPL-3.0 Go transport host, pinned NoizDNS
(MIT), the DNSTT fork (AGPL-3.0), Rovunet soar-mobile (project AGPL-3.0),
getlantern/soar v0.2.4 (Apache-2.0), golang.org/x/crypto/ssh and Gorilla WebSocket
(BSD licences). Full corresponding host/module sources are included in the
review source archive, with vendored dependencies and their own licence files.
See transports/vendor/modules.txt and licenses/Go/INDEX.json.

NaiveProxy v154.0.8037.49-5 is an unmodified separately executed official binary.
Source: https://github.com/klzgrad/naiveproxy/tree/v154.0.8037.49-5
The upstream binary-package licence is retained at licenses/NaiveProxy-LICENSE.txt;
Chromium and other incorporated components retain their respective licences.
See engines/transports-pins.json for exact download and executable hashes.
No VayDNS, Slipstream, Wintun or WireGuard driver is redistributed in preview.2.
WireGuard here is the existing userspace protocol inside official Xray.

Preview.3 includes the CC0 VayDNS client taken from anonvector/vaydns
45f18f16af40b6ad666e8119be08701b21395ee2 and the corresponding net2share
KCP fork. See transports/deps/vaydns/COPYING, transports/go.mod and licenses/Go.
The vaydns-mobile wrapper is not used. The separately executed official
Slipstream client comes from Mygod/slipstream-rust v0.1.1
(c7b265c11dd13d6dfd601bc81d54d61ee9bdde23). Source and binary pins are in
engines/transports-pins.json. See licenses/Slipstream-* for Apache, picoquic,
picotls and OpenSSL attributions. No server executable is installed.

Preview.4 bundles the unmodified static Vazirmatn Regular font v33.003 by
Saber Rastikerdar and contributors under SIL Open Font License 1.1.
See licenses/Vazirmatn-OFL.txt and Assets/font-provenance.json in the source.

Preview.5 uses ZXing.Net 0.16.11, Copyright ZXing.Net authors and Michael Jahn,
under Apache-2.0. Source: https://github.com/micjahn/ZXing.Net
See licenses/ZXing.Net-Apache-2.0.txt and the pinned NuGet package lock.

Four other client interfaces were reviewed for general design patterns only:
v2rayN, Hiddify, Clash Verge Rev and FlClash. No source, icons or assets from
those interfaces are incorporated. Exact reviewed paths/blob hashes and the
license distinctions are recorded in docs/CLIENT-SOURCE-REVIEW.json.

## Windows preview 6

The five connection-state shield geometries and six resolver/country data files
come from Rovunet Android v0.3.0-alpha16 (AGPL-3.0). The original SlipNet
attribution and Android notice are retained in docs/references/ANDROID-NOTICE.md.
Paths and SHA-256 are in docs/SOURCE-REFERENCES.json. Windows scanner, Prism
request handling, bundle interoperability, UI and process coordination are new
Windows implementations. The DNS wire codec is the existing pinned DNSTT codec.
The NoizDNS client and CC0 VayDNS client contain the local Windows adaptations
listed in transports/deps/MODIFICATIONS.md. Both vendored and dependency copies
include those changes. Upstream notices and licenses remain in licenses/.

## Windows preview 13

The structured core-profile field set and editor behavior follow the local
Rovunet Android v0.3.0-alpha17 CoreForm.kt, CoreLink.kt and CoreProfileEditor.kt
(AGPL-3.0). Reference hashes are recorded in docs/SOURCE-REFERENCES.json.
Windows form hosting, loss-preserving native/URI editing, metadata persistence,
fixed card layout, measurement generations and cancellable Agent coordination
are Windows implementations. Existing Android/SlipNet notices remain in place.
