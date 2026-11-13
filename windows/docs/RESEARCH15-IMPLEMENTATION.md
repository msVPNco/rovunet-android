# Researched continuation 15 of ACTIONS03 — local preview.10 / pilot18 / tools4

This is a local review, not ACTIONS04, a public release, or full native acceptance.
Preserve consumer75aa (preview.9), native640fa (pilot17), tools3 3c9e and their failures.

## Maintenance recovery
Tools4 records all image DACL preimages, NTFS volume/file IDs, SHA256, the canonical installation directory ID, installation receipt and service image/account/state before changing the service or ACLs.
The SYSTEM/administrator-only journal, operation lock and independent recovery scripts live under ProgramData/Rovunet-Network-Maintenance, outside the deletion/Execute-denied tree.
Each phase is written through and atomically replaced. A separate deletion-requested bit survives restoration failures and phase changes.
Unknown installations/file replacements do not receive old ACLs. Restoration attempts every file and saves per-file errors. Service restart additionally checks its exact image and account.
DeleteService intent, marked-for-deletion polling, irreversible state and reinstall-required are distinct. External handles/timeouts preserve files and journal.
Restart Manager discovers file users; no RmShutdown, RmRestart or general force-kill is used.
A host-local child was killed after its first real ACL mutation; independent-process recovery restored both original access-rule sets. This is not SCM remove/reinstall or physical power-cut acceptance.

## Native lab: pilot18, uninstalled and opt-in
New original code constructs four BOOTTIME and four PERSISTENT block filters with separate keys under an installation-specific sublayer. Flags are never combined.
Fresh isolated installation receipts include installId. Boot activation is an explicit elevated lab command:
Rovunet.NetworkBroker.exe --boot-arm --accept-isolated-lab
The existing service must be stopped (global broker mutex); root, machine/lab receipt, manifest and private ACL validation remain required.
An arming receipt precedes the WFP transaction. BFE runtime blocks remain until the ordinary persistent protection transaction has committed; only then is their four-filter runtime set removed transactionally.
Boot definitions remain armed for the next reboot. Explicit network recovery disarms its matching boot receipt before removing ordinary protection; standalone --boot-disarm is available only without an active network journal.
New readiness reports distinguish a receipt from verified current runtime filters and never certify actual pre-BFE boot capture. Foreign GUID/sublayer/filter identity conflicts fail closed.
Maintenance gate blocks new service starts. Request identity is rechecked from the real pipe impersonation token after reading each request and is bound to PID/start time/session/SID for the connection.
Used start-session GUIDs are durably recorded per installation receipt before privileged changes; old nonces are not evicted silently.
None of these source/build checks accepts actual WFP boot, SCM, leak transitions, SID/replay/race or power behavior. The VM stays paused; physical-host guards remain. Consumer TUN is off and UDP lab opt-in/off by default.

## Consumer
Updater uses monotonically increasing route generation for client/state/profile/port transitions, event cancellation, and an atomic generation check immediately before replacing its destination.
Fast A->B->A cases and the verified-payload/precommit boundary are tested. No direct fallback or installer autoexecution is added.
Hero status now uses current HTTPS latency evidence instead of only the initial Agent verified flag; plain connected and observed-data messages do not fabricate internet verification.
Metadata remains optional/nullable. Bad/missing advisory refresh keeps profiles and labels previous usage cached. Dedicated status wins over generic usage on restart and refresh.
The migration model is preview-only, not an approved cross-platform schema and has no Apply method. It includes source/account/period, ID membership and endpoint binding changes, override/selection/trust flags. Name/position never match items and trust transfer defaults false.
Header and scanner ellipsis use the same filled vector dots as cards. Positions and actions are unchanged.
Marquee clocks are removed when the owning window loses activation or is minimized, then recreated/resumed on activation. Network/counter clocks are unchanged.

## DPAPI diagnosis
A separate synthetic diagnostic records native flags/results, immediate GetLastError, process/effective/thread identities and profile at API boundaries.
Four random payload cases compare direct Win32 and .NET ProtectedData(CurrentUser). The diagnostic never changes product crypto or scope.
.NET internal native flags are not instrumented; the report says so rather than reporting a guessed runtime flag.
A guarded SYSTEM-to-LocalService matrix script is prepared separately. It has not been rerun in this stage and the earlier service-specific decrypt result remains open.
SQLite/accounting remains Panel-owned. Device counters, advisory usage and authoritative status remain different sources.

## References and limits
https://learn.microsoft.com/en-us/windows/win32/api/fwpmtypes/ns-fwpmtypes-fwpm_filter0
https://learn.microsoft.com/en-us/windows/win32/fwp/object-management
https://learn.microsoft.com/en-us/windows/win32/fwp/wfp-error-codes
https://learn.microsoft.com/en-us/windows/win32/rstmgr/using-restart-manager
https://learn.microsoft.com/en-us/windows/win32/api/winsvc/nf-winsvc-deleteservice
https://learn.microsoft.com/en-us/windows/win32/ipc/impersonating-a-named-pipe-client
Mullvad's pinned researched implementation was compared as an architectural example; no upstream implementation was copied into this change.
Full native/boot/SCM/physical/peer/live-download/publisher acceptance and the common migration contract remain open.
