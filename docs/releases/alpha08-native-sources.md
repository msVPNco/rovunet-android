# Native source provenance

Application Kotlin/resources and C/C++ sources are in this repository. The APK retains the previous native binaries; this repair does not claim a fresh native rebuild.

The release source asset expands available pinned Git submodules and the exact lyrebird revision retrieved through the Go module proxy. `SOURCE-MANIFEST.json` records their commit IDs. Native programs fetched by upstream build scripts keep their original source URLs and licenses.

One upstream tree could not be retrieved for this release: `dnstt-mobile` at `b265e04c40835ec1bacd447fff79169e6c2c2233`, originally `https://github.com/anonvector/dnstt-mobile.git`. The original URL requires unavailable access; the two public repositories found under that name did not contain the pinned commit. They were not substituted. The Git link and original revision are retained. The source archive is therefore the complete application snapshot **plus available native sources**, not a fully self-contained native source distribution. Reproducing the unchanged JNI dependency needs that original source tree.

No signing keys, build credentials or user configuration backups are included in the release source asset.
