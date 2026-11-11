// Local concurrent TLS1.3 target for REALITY integration testing.
package main
import("crypto/tls";"net/http";"os";"log")
func main(){ s:= &http.Server{Addr:os.Args[1],TLSConfig:&tls.Config{MinVersion:tls.VersionTLS13},Handler:http.HandlerFunc(func(w http.ResponseWriter,r *http.Request){w.Write([]byte("test target"))})};log.Fatal(s.ListenAndServeTLS(os.Args[2],os.Args[3])) }
