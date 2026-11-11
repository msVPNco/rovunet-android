# Portable import corpus

`cases.json` is a frozen set of **synthetic** samples, independently assembled from the documented positional wire formats. It is not a collection of binaries or exports captured from every historical release. All hosts, accounts, secrets and device IDs are artificial. No sample is used to open a connection.

The 50 samples contain:

- 33 readable examples, one for each supported version v1–v33;
- 7 restricted examples preserving lock/hash, expiry, sharing policy, device binding, hidden resolvers, collection metadata and v33 delimiter-safe credentials where supported;
- 6 explicitly unsupported versions, including v34 with a hypothetical required security extension, integer overflow and nonnumeric tags;
- 4 invalid/truncated security payloads that must be rejected.

`ImportCorpusTest` reads these fixed inputs through the actual `ConfigImporter` in the standard Gradle unit-test source set. `WireBase64` is the same production codec in the app and these tests; no Android Base64 or crypto stub is used by that Gradle test. Its output is checked against independent standard vectors and the JDK codec. The existing Bouncy Castle dependency provides the implementation on all supported Android API levels. `LockHash.isValid` is called directly instead of initializing the native encryption library just to validate a hash's syntax.

The contract is asymmetric: a new reader continues to understand explicitly supported formats; an unknown version is rejected with an explicit diagnostic. **An old reader is not required to understand a new format.** A mixed import may retain supported rows with a warning, but must never convert an unsupported row into a usable profile. The same policy applies after a password bundle is decrypted. Supported lock, device and expiry fields must retain their values, rather than silently reverting to permissive defaults.

Run `:app:testFullDebugUnitTest`. The legacy JVM script also runs these same test classes and resources, alongside its existing contracts; that auxiliary harness still has Android/JNI boundary stubs for unrelated legacy tests. The frozen corpus must not be regenerated from the exporter being tested. Add samples intentionally when extending the format, including its negative/security cases.

Limits: this is a baseline, not exhaustive dialect coverage or proof that every malformed historical optional field is rejected. Native `rovunet-enc://` encryption/decryption requires its JNI library and is not executed by these JVM tests; it uses the same version dispatcher after decryption. Real server operation, Android Keystore and the device-specific VPN lifecycle remain device/integration tests.
