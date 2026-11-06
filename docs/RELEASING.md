# Release procedure

## Production signing certificate

For a public build, provide `storeFile`, `storePassword`, `keyAlias` and `keyPassword` in the private root `keystore.properties`. A relative store path is resolved from the Android app module. Keep the keystore and its passwords outside version control.

Use the intended signing alias to compute the SHA-256 of the DER certificate on a trusted machine. Let `keytool` prompt for the password.

```bash
keytool -exportcert -keystore /secure/path/release.jks -alias release -file release-cert.der
sha256sum release-cert.der
```

The authoritative digest is versioned in `config/release-signing-certificate.sha256` and documented in [SIGNING-IDENTITY](SIGNING-IDENTITY.md). Optional repository variable `ROVUNET_RELEASE_CERT_SHA256`, local environment or `-ProvunetReleaseCertSha256=…` must match that pin; they cannot silently approve a new key. Recover the existing production key after a workspace reset; never generate a replacement merely because the local keystore is missing. Compare it with the certificate digest from `apksigner verify --print-certs` on the distribution APK. For a signer change, plan and verify the Android update path for existing installations before changing this value. The emulator job uses the independently signed candidate package and does not use these credentials.

## Artifact identity

The alpha11 Full APK and explicit source assets use application commit 91f2c0e9a9fd94eb72e30236bc965293b5303762. The release tag includes later documentation/workflow changes; they do not rebuild the APK. Record both identities when packaging.

Alpha14 uses the managed production identity from SIGNING-IDENTITY.md and versionCode 25; private validation is alpha14-review02 and is not a public update.

For a new application version, update its version code/name, complete relevant checks, record the exact source revision, and verify package, version, signer, native payload and personal-data scan. Do not replace an existing published APK with a different binary under the same version.

## Verification and automation

Pushes, pull requests and version tags run the verify job. Android native builds are an explicit workflow_dispatch choice, because they require original submodule access, SDK/native dependencies and signing material. They do not automatically create a release or overwrite public assets.

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
