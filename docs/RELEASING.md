# Release procedure

## Mandatory phone review

The owner requires an installable APK for phone testing before public release.
CI, emulator success and delivery of that APK are not publication approval.
Preserve the production package/signer for an owner test intended to update the
installed public app; identify it as an unpublished review build in its notes.
Keep private instrumentation candidates separate. After explicit acceptance,
publish the exact accepted APK and update current README variants, STATUS,
user/upgrade/build guides, release notes and provenance together. Historical
records remain historical; never relabel an untested binary as accepted.

## Production signing certificate

For a public build, provide `storeFile`, `storePassword`, `keyAlias` and `keyPassword` in the private root `keystore.properties`. A relative store path is resolved from the Android app module. Keep the keystore and its passwords outside version control.

Use the intended signing alias to compute the SHA-256 of the DER certificate on a trusted machine. Let `keytool` prompt for the password.

```bash
keytool -exportcert -keystore /secure/path/release.jks -alias release -file release-cert.der
sha256sum release-cert.der
```

The authoritative digest is versioned in `config/release-signing-certificate.sha256` and documented in [SIGNING-IDENTITY](SIGNING-IDENTITY.md). Optional repository variable `ROVUNET_RELEASE_CERT_SHA256`, local environment or `-ProvunetReleaseCertSha256=…` must match that pin; they cannot silently approve a new key. Recover the existing production key after a workspace reset; never generate a replacement merely because the local keystore is missing. Compare it with the certificate digest from `apksigner verify --print-certs` on the distribution APK. For a signer change, plan and verify the Android update path for existing installations before changing this value. The emulator job uses the independently signed candidate package and does not use these credentials.

## Artifact identity

Current alpha16/code32 publishes the exact owner-accepted review APK, approved on 2026-11-08: SHA-256 `3950b602865c55a6dfc8bec4cbb13da4585dca0c28ad4842afd6d8b273618b7a`, application commit `ea5558fea6c6a2e9f2b94ef1ce8a230bd2308ba4`. [CI](https://github.com/msVPNco/rovunet-app/actions/runs/42471804819) passed 83 portable/JUnit, 86 Gradle unit, 55 Debug emulator and two minified Release JNI cases. Publication tooling/docs are recorded separately and do not rebuild/re-sign the accepted APK. Detailed battery, overnight/Doze and physical-network handoff measurements are still outstanding. Alpha15/15.4 paragraphs below are historical.

The alpha11 Full APK and explicit source assets use application commit 91f2c0e9a9fd94eb72e30236bc965293b5303762. The release tag includes later documentation/workflow changes; they do not rebuild the APK. Record both identities when packaging.

Alpha15 uses the same managed production identity as alpha14 and versionCode 26. Its application build commit is `908253b42082bbd80007677f0369792d324a378f`; [CI run 42215030490](https://github.com/msVPNco/rovunet-app/actions/runs/42215030490) passed verify, 50 JVM tests, 31 private Debug device cases and two minified private Release JNI cases. Source/archive and release documentation revisions are recorded separately; the approved signed APK is not rebuilt for documentation changes.

For a new application version, update its version code/name, complete relevant checks, record the exact source revision, and verify package, version, signer, native payload and personal-data scan. Do not replace an existing published APK with a different binary under the same version.

## Verification and automation

Code pushes, pull requests and version tags run verification and the reusable Android device gate. That gate builds private Debug/minified Release APKs and a public unsigned APK. The separate full native/signing build remains an explicit workflow_dispatch choice. Documentation-only publication commits may use [skip ci] after checking application/build/test inputs against the recorded passing source. The ordinary build workflow does not publish. The narrowly scoped alpha16 publication workflow is enabled only by the owner-approved release branch; it restores and verifies the exact accepted APK, uploads a draft, checks every asset digest, then publishes Latest. It has no signing key and cannot publish a different version or rebuild the accepted APK.

For alpha11 the exact application commit has a successful verify run; the APK was built locally. Do not describe a skipped Android CI build as a successful Android CI build. A tag verification run covers the release documentation/workflow revision separately.

## Publish checklist

1. Prepare the verified Full APK, source ZIP and lightweight source ZIP.
2. Include the Persian guide, verification JSON, APK and signer SHA-256 records. Generate SHA256SUMS.txt over every other final asset.
3. Update README.md, README.fa.md, current notes, status and user/build guides. Keep historical release records labelled as historical.
4. Merge the release branch after checking that application sources still match the recorded artifact revision.
5. Create a draft release for the version tag at the reviewed release commit. Attach assets, wait for all uploads, and check exact filenames and sizes.
6. Publish and explicitly set Latest for the public in-app update channel. Alpha11's tag retains the established version naming; the GitHub release is not marked prerelease.
7. Verify GitHub's asset digests against SHA256SUMS.txt, the Latest endpoint and the published source/version links. The updater needs a compatible Full APK and its checksum/digest; do not advertise a Lite update without a Lite artifact.

## Accuracy and follow-up

Retain license notices, native provenance and limitations. Do not claim a fresh native rebuild, production signer, physical-device validation, store publication or a fully translated interface unless they were actually completed.

Known phone feedback remains tracked in STATUS.md. Public release authorization does not turn unperformed device tests into a pass.

## Offline signing preparation

If no production keystore is available, prepare the non-installable Release using
`-ProvunetUnsignedPublication=true -PveyraPrebuiltNative=true`. The output name
ends in `-unsigned.apk`; private-candidate mode is mutually exclusive. Never
rename or publish it as the final Full APK. Ordinary public signed builds keep
the production certificate gate and never substitute a debug signer.

The historical alpha11 private key is not in the workspace. The owner explicitly
authorized a new production identity before deployment on 2026-11-06; it does not
provide an in-place alpha11 upgrade. Preserve locked/device-bound profiles and retain the readable
old installation until authorized exports/reissue are verified. See UPGRADE.md.

For alpha15, the publication commit changes documentation only. Its application/build/test inputs must compare byte-identically with verified application commit `908253b42082bbd80007677f0369792d324a378f`. A documentation-only `[skip ci]` commit is recorded as not rerun, never as a passing new device run; the binary evidence remains run 42215030490. Run the local documentation/link checks before publication.

## Owner-approved alpha16 publication

`publish/alpha16-approved` (or a numbered `-retryN` branch after a packaging-only correction) is a one-version, explicit publication trigger after owner acceptance and merge. `tools/release/publish_approved_alpha16.py` verifies application/build/test inputs against the successful source, the pinned CI artifact, public APK copy record, production certificate and every final asset. It creates a draft, attaches the Full APK, full/light explicit sources, guide, R8 mapping, CI evidence, certificate/provenance and hashes, verifies server digests, then sets this release Latest. It refuses to replace an existing public release or delete existing assets. No private key, recovery material, unsigned or test APK is published. This authorization does not apply to future versions.

## Shared repository after rename

`msVPNco/rovunet-app` is the canonical repository for all app platforms. Keep
Android `v<version>` tags; use `windows-v<version>` and `macos-v<version>` for
new desktop releases, with platform-specific source/provenance, assets, hashes
and signing identities. Global Latest can belong to any platform. Android
download links are pinned to its released tag; its updater must inspect published
releases for a compatible Full/Lite APK and the Android preview preference.
See [platform policy](PLATFORMS.md).

The repository rename does not change installed APK bytes or their embedded
update allowlist. Alpha16/code32 needs a direct install of the forthcoming
compatibility APK after owner review; repository redirects cannot repair its
embedded old URL check. Do not replace the published code32 APK or rewrite its
source/checksum/provenance assets. An unpublished compatibility build must have
a new version/code and retain the fixed Android package and production signer.

Historical source/provenance JSON, the public APK copy record and archived PDF
editions remain evidence of their original release. Human-readable navigation
URLs may be updated to the canonical location without changing historical test
results or claiming that those binaries contain the new updater.
