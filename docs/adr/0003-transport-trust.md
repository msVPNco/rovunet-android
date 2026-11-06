# ADR 0003: CA validation, endpoint-scoped TOFU and TLS SPKI

Status: accepted and implemented.

Ordinary publicly trusted TLS certificates use system CA validation and hostname checks. A user-requested connection/test may ask for explicit approval of an otherwise untrusted, valid certificate; background scans cannot approve an unknown identity. SSH pins remain endpoint-scoped host-key hashes.

TLS TOFU pins now hash the leaf public key's DER SubjectPublicKeyInfo, with a versioned storage namespace. Certificate renewal with the same public key is accepted, subject to validity and hostname checks. A changed public key is rejected; SPKI does not eliminate the need for an approved key-rotation procedure and is not unconditionally stronger than leaf pinning.

An existing leaf pin migrates only while the exact previously approved certificate is presented. The migration atomically installs its SPKI pin and removes the leaf pin. If renewal already occurred before migration, the old leaf pin cannot safely reveal the old public key; the application refuses rather than silently trusting a replacement. Reset/re-approval remains an explicit user action.

Local TLS integration tests cover exact legacy migration, renewed certificate with the same key, a changed key, hostname validation and a legacy mismatch. Production endpoint certificates are never rewritten by these tests.
