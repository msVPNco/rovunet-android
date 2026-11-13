# Bounded maintenance backport

Base: MetaCubeX/mihomo `3f27ef39f8c96bbfbc8b0b9bb10f5c9ef60149d8` with the existing Rovunet helper entry points.

Apply `6780eeeb.patch` using `git apply` in a fresh base directory. It contains the upstream fix and its exhaustive domain-length regression, unchanged in meaning. Official source: https://github.com/MetaCubeX/mihomo/commit/6780eeeb27c7eda5e79a6310a4f7f91ba7cb725d . The old byte arithmetic wraps for domain lengths254/255. Both helper entry points use this inbound parser, behind random local authentication.

Rebuild both `cmd/rovunet-awg` and `cmd/rovunet-extra` with Go1.27.3, CGO_ENABLED=0, `-trimpath -buildvcs=false -ldflags='-s -w'`. Verify the hashes recorded in supplemental-pins.json before packaging. Run upstream address-length tests, encrypted TCP/UDP relay and actual Windows Agent fixtures. The complete patched corresponding source is shipped as a separate archive. Native libraries are not silently upgraded to a new release.

The existence of this backport is not a claim that every future advisory has been excluded. Review upstream releases on the next iteration.
