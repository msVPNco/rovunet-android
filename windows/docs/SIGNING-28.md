# Publisher signing preparation

No valid code-signing certificate with a private key was found in the current user's or local machine's personal stores on2026-11-12. The local installer remains unsigned. No account was created, certificate purchased, private key exported or root installed.

prepare-offline-signing.ps1 copies an explicitly hashed Rovunet EXE to a fresh stage and writes a signing request without mutating delivered bytes. sign-staged-artifact.ps1 requires an explicitly approved publisher subject/thumbprint, a private code-signing certificate with a trusted chain, signtool and HTTPS RFC3161 timestamp endpoint. It validates publisher identity, signature and timestamp and emits new hashes. Neither script auto-enrolls a publisher or rotates its identity. The signature script is prepared but not executed without that real identity.

For inner binaries: sign the own-product executables first, then rebuild the payload/backend/outer installer and sign the final outer artifact. Do not re-sign third-party engines under a fabricated publisher identity. Re-run installation/upgrade/recovery against the exact signed final bytes; regenerate hashes/manifests and distinguish these from the unsigned local review. Do not change bytes after signing.

Local Get-AuthenticodeSignature success alone is not a claim of public trust on other machines. Independently verify approved publisher chain/revocation/timestamp under a clean Windows policy. A self-signed root, EV reputation promise or GitHub SHA256 is not a substitute for this acceptance. App updater in this iteration downloads only and never executes unverified code.
