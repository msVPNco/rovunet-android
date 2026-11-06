# ADR 0005: credential availability during locked-screen recovery

Status: retain the present background-capable device key.

A VPN must be able to reconnect while the phone is locked and during long background sessions. Requiring user authentication or an unlocked device for every credential operation would turn a screen lock into a recovery failure. Android also documents key deletion/availability bugs with unlocked-device-required keys on Android 12–14.

Keep AES-GCM with an installation-specific Android Keystore key, no application backup, no plaintext credential columns, and explicit recovery for unreadable sealed rows. This is protection at rest, not a guarantee against a compromised running application or unlocked phone.

An optional foreground vault-lock mode could use an authenticated key, with clear restrictions on boot/background recovery and a tested migration. StrongBox can be evaluated separately with fallback and measurements; it is not silently forced during this connection repair.

Reference: https://developer.android.com/reference/android/security/keystore/KeyGenParameterSpec.Builder#setUnlockedDeviceRequired(boolean)
