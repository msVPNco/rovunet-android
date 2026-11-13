# Read-only panel contract handoff

Windows local0.1.2 has capabilities beyond public0.1.1. Use contracts/windows-capabilities-v1.json as a versioned OUT-OF-BAND negotiation proposal, not a new feed key. No Android/Mac/panel files or live server were modified, and no message was sent to their owners.

Public0.1.1 clients must continue receiving the established strict version1 envelope. Unknown extra keys, including min_app_version/capabilities, are deliberately rejected. Four simple fixtures in contracts/feed-fixtures-v1.json plus the existing parser/account/protected/identity tests preserve this boundary. Introduce a separately negotiated schema/version before changing it.

Do not filter newer Windows AWG/AnyTLS/TUIC/Mieru solely because the old public matrix says absent. Conversely, do not advertise a family merely because a link parsed: respect the local/public app version, dialect/options, architecture, pinned engine, network mode and account restrictions. AWG options are never downgraded into stock WireGuard. Unknown effective XHTTP/SS2022 options are reported rather than discarded. Keep profile/item/source IDs, local overrides and account identity stable on refresh. Mahsa aliases now migrate internally to the direct encrypted feed while keeping sourceID.

Quota is server/account data, distinct from local device bytes. Missing metering/expiry is unknown, not unlimited. Existing account status version1 monotonic revision, identity renewal, date/freshness and null rules remain unchanged. No invented quota and no account/ping gate on manual connect.

Pending integration: panel maintainer should run these fixtures and choose capability negotiation for old/new clients before changing their export filter. This file is a reviewable handoff artifact, not a claim of panel deployment.
