module rovunet/transports

go 1.27.3

require (
	github.com/getlantern/soar v0.2.4
	github.com/gorilla/websocket v1.5.3
	github.com/net2share/vaydns v0.0.0
	github.com/sirupsen/logrus v1.9.4
	golang.org/x/crypto v0.52.0
	golang.org/x/net v0.54.0
	noizdns v0.0.0
	soar-mobile v0.0.0
	www.bamsoftware.com/git/dnstt.git v0.0.0-00010101000000-000000000000
)

require (
	github.com/andybalholm/brotli v1.2.0 // indirect
	github.com/flynn/noise v1.1.0 // indirect
	github.com/klauspost/compress v1.18.3 // indirect
	github.com/klauspost/cpuid/v2 v2.3.0 // indirect
	github.com/klauspost/reedsolomon v1.13.0 // indirect
	github.com/pkg/errors v0.9.1 // indirect
	github.com/refraction-networking/utls v1.8.2 // indirect
	github.com/tjfoc/gmsm v1.4.1 // indirect
	github.com/xtaci/kcp-go/v5 v5.6.61 // indirect
	github.com/xtaci/smux v1.5.50 // indirect
	gitlab.torproject.org/tpo/anti-censorship/pluggable-transports/goptlib v1.6.0 // indirect
	golang.org/x/sys v0.45.0 // indirect
	golang.org/x/text v0.37.0 // indirect
	golang.org/x/time v0.14.0 // indirect
)

replace noizdns => ./deps/noizdns

replace soar-mobile => ./deps/soar-mobile

replace www.bamsoftware.com/git/dnstt.git => ./deps/dnstt

replace github.com/net2share/vaydns => ./deps/vaydns

replace github.com/xtaci/kcp-go/v5 => github.com/net2share/kcp-go/v5 v5.0.0-20260325165956-416ba9d3856d
