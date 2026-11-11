# Rovunet 0.3.0-alpha09 — Full

Version code **15**. Supersedes alpha08.

A final regression exposed a queued-stop race: after Disconnect, a new UI Connect request could wait for the old service to stop, while the old service rejected its pending stop because the manager already had the newer request ID. The barrier could never complete.

Stops now target the last CONNECT actually delivered to the Android service instance. A queued request cannot invalidate that stop; a newly delivered CONNECT still rejects old teardown. System-restored starts also record their delivered request ID. Undelivered requests can be canceled without creating a stop barrier for a service that does not exist. They cannot start later after cancellation. New connects are rejected until active teardown is acknowledged. The real-manager regression now covers a stop delivered after a newer connect is queued, in addition to stale callbacks and a stop delivered after a new connect has started.

Core startup also waits for an actual local SOCKS response rather than treating a process alive after 200 ms as ready. Port conflicts are checked before launch, startup is bounded, and output readers are joined during stop. Regressions cover a deliberately delayed listener a process that never becomes ready, and a child that ignores graceful termination and must be forcibly stopped.

All [alpha08 repairs](alpha08.md) are retained: config preservation, identity verification, authenticated HTTP sharing, domestic routing, worker-thread import/crypto, timestamped diagnostics, cache invalidation, protocol-specific test deadlines, verified in-app updates and the R notification mark. See the [20-item verification record](alpha08-verification.md).

The Full Release APK is not debuggable and retains the previous signing identity for upgrade compatibility. Physical-device validation and production signing remain pending. Existing native binaries are retained. The [native source provenance limitation](alpha08-native-sources.md) is unchanged.

Telegram: https://t.me/RovunetVPN

Optional development support — USDT, Tron/TRC20 only: `TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ`.
