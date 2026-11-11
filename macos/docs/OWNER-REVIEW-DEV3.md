# Target Mac feedback on dev.3 — 2026-11-11

The owner supplied three recordings (approximately 51, 42 and 189 seconds).
Their images were reviewed through contact sheets and detailed frames. Audio
was extracted and transcribed locally with multilingual Whisper models; direct
audio input is unavailable in this chat. Transcription is supporting evidence,
not a verbatim certified transcript. Raw media, transcripts and frames stay
outside the repository and delivery directories. No private endpoints, accounts,
contacts or device identifiers are copied into this document.

## Observed and reported

* Recording A, about 00:22: Finder Quick Look displays bundle version 0.1.0.3.
  The installation window still shows small icons and a plain background.
  Dragging is unsuccessful in the recording. Copy/paste into Applications is
  shown near the end. The exact cause of failed dragging is unestablished;
  Quick Look appearing alone does not prove a filesystem or trackpad defect.
* Recording B, about 00:04: macOS asks to authorize Rovunet.Agent to modify the
  system network configuration. This is a System Proxy authorization dialog.
* Recording B, about 00:32: a separate login-Keychain dialog names service
  org.rovunet.macos.development. Always Allow, Deny and Allow are visible. The
  recording does not establish that Always Allow was selected successfully.
* Recording B, about 00:09–00:29: the owner reports that the R menu-bar icon
  does not open the expected show/connect/quit actions. This remains a target
  Mac defect; headless native-menu command checks did not validate AppKit.
* Recording C, about 00:09–00:19: the owner reports that the top section and
  language improved, ordinary configurations work, and a Soar configuration
  worked. These are limited owner reports, not all-family acceptance.
* Recording C, about 00:27 and 00:48: System Proxy mode is shown, followed by
  a persistent ArgumentException notice. Its originating stack cannot be
  established from the picture. Do not attribute it to a particular resolver,
  protocol, authorization or JSON schema without diagnostic evidence.
* Recording C, about 00:41–00:49: the owner asks for local-network DNS access.
  A Network resolver (Local) action is visible. Its presence did not make the
  action discoverable or establish successful macOS resolver discovery.
* Recording C, about 00:58–01:48: entering the scanner displays prior results;
  finding scan settings, banks and start controls is unclear to the owner.
* Recording C, about 01:59–02:43: the failed-results filter is selected. Clicking
  a list row produces the theme's bright green selection even when its status
  shows no DNS/tunnel/HTTPS success. The result's checkbox remains unchecked.
  File export then displays the generic Enter a valid IP address message.
  Clicking the row is not the scanner's checkbox selection. Both the visual
  feedback and the error text are misleading.
* The existing result action creates a new profile; it does not send a chosen
  resolver back into the original editor. The owner explicitly wants an obvious
  apply/confirm action near the top, returning results to the originating form,
  and familiar Windows/mobile navigation. Existing draft fields must survive.
* The owner also requests less vertical window space on the 14-inch display.
  This is a layout preference, not a request to scale all text smaller.

In the accompanying newer message, the owner says that installing the TUN
service stopped the fingerprint requests, while quitting/reopening still asks
for a password. This later report must not be conflated with the earlier
System Proxy dialogs in the recordings. The recordings do not establish
post-installation system-wide TCP/UDP/IPv6/DNS traffic or routing cleanup.

## Immediate Keychain guidance

Run the installed /Applications/Rovunet.app. For the dialog naming the above
service, the normal login-Keychain password authorizes access. Apple's
documentation distinguishes Allow (one access) from Always Allow (persistent
consent for the app). The latter is the appropriate option for a trusted copy
of this private build. Quit and reopen that same installed build to verify.
Do not promise that permission survives a changed ad-hoc build or a separately
locked login keychain. Do not delete the encryption key, user data, or grant
all applications access.

Primary source:
https://support.apple.com/en-gb/guide/mac-help/kychn002/26/mac/26

## Follow-up work, not completed fixes

1. Verify persistent consent for the exact installed app and identity. Keep the
   existing GUI-only Keychain cache and encrypted inherited-pipe behavior.
2. Repair native menu behavior with target Mac evidence; distinguish opening the
   status menu from executing its items. Do not claim that a command-model test
   reproduces either failure.
3. Add an editor-return/apply flow for selected usable DNS results, with visible
   selection count, clear success/failure colors, informative empty-selection
   actions, prominent bank/local source choice, and familiar scanner navigation.
4. Diagnose ArgumentException through safe method/type metadata, retaining the
   original failure context. Never export raw profile or exception messages.
5. Revalidate Finder presentation and dragging on macOS; cross-host DS_Store
   structure checks were insufficient to establish the actual installation UI.
6. Keep TUN network acceptance pending until the exact post-installation package
   has demonstrable application traffic and restoration on the target Mac.

No application binaries or frozen dev.3 delivery bytes were changed by this
review. A new candidate must have its own version and matching verification.
