# Pinned Windows supplemental adapter

Engine source: MetaCubeX/mihomo, commit `3f27ef39f8c96bbfbc8b0b9bb10f5c9ef60149d8`, plus upstream SOCKS fix6780eeeb27c7eda5e79a6310a4f7f91ba7cb725d. Apply native-patches/mihomo/6780eeeb.patch before building. The complete corresponding source archive accompanies the local installer. Copy this directory into `cmd/rovunet-extra` in that exact source checkout. Go 1.27.3, GOOS=windows, GOARCH=amd64, CGO_ENABLED=0:

```text
go build -trimpath -buildvcs=false -ldflags "-s -w" -o rovunet-extra.exe ./cmd/rovunet-extra
go test -count=1 -v ./cmd/rovunet-extra
```

The adapter separates AnyTLS/TUIC/Mieru from the AWG child; both are rebuilt with the bounded SOCKS fix in preview.5. C# SupplementalLinks follows Android SupplementalCore and the read-only macOS adapter at repository revision 5e66f167c85aecac26008d930a7a24df6f2a31f2. The native relay fixtures derive from Android's tools/native/awg/supplemental_test.go at the same revision. Windows tests use process termination instead of Unix interrupt semantics; fixture certificate roots are scoped to Go test child processes. The production helper has no fixture certificate environment switch. Additional Windows Agent fixtures are activated by ROVUNET_PROTOCOL25_RUNNER_DLL and ROVUNET_TEST_DOTNET; unset variables do not substitute for delivered-package verification.

The public Xray SOCKS endpoint is TCP-only. The helper's authenticated UDP association remains private. Rebuild hashes are recorded in engines/supplemental-pins.json and compiled into Core/EnginePins.cs. Final payload binaries must match these pins.
