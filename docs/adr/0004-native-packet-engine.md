# ADR 0004: remove the dormant Kotlin packet engine

Status: accepted and implemented.

The active packet engine is hev-socks5-tunnel, with the established descriptor and effective MTU carried together. KotlinTunnelManager, TunnelConnection and TcpPacketBuilder formed an unselected alternative packet implementation with no live manager caller. Keeping a second untested implementation in the APK obscured the active MTU and TCP paths.

Remove those three files. Their top-level ResolverConfig data class was also used by the live native Slipstream bridge and scanner; retain its original fields/signature in ResolverConfig.kt. Removing a dead class does not imply that every declaration in the same source file is dead.

A future alternate engine must implement an explicit engine contract and pass the same cross-UID traffic, MTU, shutdown and recovery device gates before it can be selected. Preserve upstream attribution in NOTICE; deleting an implementation does not delete its licence history.

Source-only follow-up after review03: remove the remaining NatTable, TunInterface and IpPacketParser files as well. Their top-level declarations have no live callers, including tests and the auxiliary JVM harness. Their old parser validation weaknesses are not an active traffic-path vulnerability; removing them prevents accidental reuse without a tested engine contract. ResolverConfig remains live and is retained.
