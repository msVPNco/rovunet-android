# ADR 0006: user selection and probe results are separate state

Status: accepted and implemented.

An explicit path choice persists per collection. Selecting another collection or returning to the application does not erase it. A connect request uses that path directly, even if a historical probe failed. Automatic initial choice skips a recent failed path on the current network, but a probe is never a mandatory precondition for a manual connection.

Temporary test sessions do not write the persistent active profile. Their measured outcomes are keyed by actual connection settings, not deletion-review snapshots, display order, timestamps or the active flag. The deletion-review token remains intact in the original model for atomic deletion; only comparison keys omit it.

Store bounded, seven-day connection history encrypted with Android Keystore, on an application-owned IO writer. Persist profile IDs, a connection fingerprint and completed measurements; never serialize a profile, its credentials, its deletion token, pending work, or discovered resolver overrides. A fingerprint is an expiring cache key, not a permanent database revision or export field. Results restored after restart or retained across a network change are historical and cannot count as current-network success.

Changing connection settings or deleting a profile invalidates that profile's evidence. An explicit Clear results action also clears the saved measurements. The UI shows each result and collection totals rather than only the fastest path. HTTP success here specifically means a valid HTTPS response with status 204; other statuses do not prove that a tunnel cannot carry useful traffic.
