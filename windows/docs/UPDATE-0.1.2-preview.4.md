# 0.1.2-preview.4 · About language switching · 2026-11-12

Only the small licence/attribution prose at the bottom of About is fixed English. The rest of the page follows the selected application language: title, channel, support copy, network caption, wallet label, action labels, accessibility names and feedback. Wallet/URL values remain exact left-to-right strings; brand/version and protocol names remain unchanged.

The defect was reproduced through actual flag-button clicks while About was open: the shell changed language, but the retained About visual tree kept its old text and reading direction. A new opt-in page callback refreshes About in place when navigation renders a changed language. Existing controls, QR source, addresses and navigation depth survive the change. Other editors are not rebuilt and their input is not discarded.

The regression fixture opens About in English and clicks Persian, Russian, Chinese and English flags in three themes and compact/wide layouts. Before the correction, 60 of 192 assertions failed. Afterward all 192 pass, including native-language captions, action labels, RTL/LTR, unchanged English credits and exact wallet copying. The pre-existing 212-check About fixture also passes, including original/exported QR decoding. Final delivered-binary and installer results live in delivery/VERIFICATION.json outside this source snapshot. Screenshots show real Persian text with English-only credits at the bottom.

Baseline: frozen 0.1.2-preview.3. Existing channel/update links, wallet QR, centering and license files are preserved. Native engines are reused without change. This update does not claim new external-server or hardware verification and is not a GitHub publication. Prior released bytes/tags and Android/macOS source remain unchanged.

For local packaging, pass versions/v0.1.2-preview.4/archives/engine-cache as EngineDirectory. The Git source contains the pinned manifests; the copied engine cache stays outside the source archive.