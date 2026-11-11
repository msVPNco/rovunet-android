# ADR 0001: configuration locks and signed exports

Status: accepted current limitation; signed export format deferred.

The legacy embedded encryption key and password hash implement a sharing policy in the official client. They cannot enforce a licence against someone who can modify the APK. Increasing password hashing cost alone does not remove that limitation. Password-encrypted bundles use their separate PBKDF2/AES-GCM format; device-local Room credentials use Android Keystore.

A future signed format must bind schema version, issuer/key ID, account, destination, device restriction and expiry into one canonical signed message. Ed25519 protects authenticity in an unmodified client. It does not prevent a modified client ignoring verification, clock rollback during offline expiry checks, or use of credentials extracted from an authorized session. Account expiry, revocation and access limits therefore also need enforcement on the server.

Do not silently convert legacy links or invent issuer trust from a key embedded in an untrusted import. Specify issuer onboarding, rotation, revocation, canonicalization and compatibility tests before changing the wire format.
