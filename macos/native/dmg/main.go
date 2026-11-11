// Creates a standard HFS+ UDIF disk image without a mounted host filesystem.
// The final image still needs an installation acceptance test on macOS.
package main

import (
    "encoding/binary"
    "flag"
    "fmt"
    "io"
    "os"
    "path/filepath"
    "strings"
    "time"

    "github.com/deploymenttheory/go-apfs-v2/pkg/disk"
    "github.com/deploymenttheory/go-apfs-v2/pkg/hfsplus"
)

func tree(source string) (*hfsplus.Entry, error) {
    info, err := os.Lstat(source)
    if err != nil { return nil, err }
    if info.Mode()&os.ModeSymlink != 0 { return nil, fmt.Errorf("unexpected source symlink: %s", source) }
    entry := &hfsplus.Entry{Name: info.Name(), Mode: 0644, UID: 0, GID: 0}
    if info.IsDir() {
        entry.Mode = os.ModeDir|0755
        children, e := os.ReadDir(source); if e != nil { return nil, e }
        for _, child := range children {
            node, e := tree(filepath.Join(source, child.Name())); if e != nil { return nil, e }
            entry.Children = append(entry.Children, node)
        }
        return entry, nil
    }
    if !info.Mode().IsRegular() { return nil, fmt.Errorf("not a regular input file") }
    f, err := os.Open(source); if err != nil { return nil, err }
    var head [4]byte; n, _ := f.Read(head[:]); f.Close()
    // Windows has no executable Unix bit. Detect Mach-O code explicitly.
    magic := binary.BigEndian.Uint32(head[:])
    if n == 4 && (magic == 0xcffaedfe || magic == 0xfeedfacf || magic == 0xcafebabe || magic == 0xcafebabf) || strings.HasSuffix(source, ".sh") || strings.HasSuffix(source, ".command") { entry.Mode = 0755 }
    entry.Size = info.Size()
    entry.Open = func() (io.ReadCloser, error) { return os.Open(source) }
    return entry, nil
}

func run() error {
    app := flag.String("app", "", "signed Rovunet.app directory")
    finder := flag.String("finder", "", "directory containing .DS_Store and .background")
    out := flag.String("out", "", "output .dmg")
    volume := flag.String("volume", "Rovunet", "Finder volume name")
    flag.Parse()
    if *app == "" || *finder == "" || *out == "" { return fmt.Errorf("--app, --finder and --out are required") }
    if filepath.Base(filepath.Clean(*app)) != "Rovunet.app" { return fmt.Errorf("expected Rovunet.app") }
    root := &hfsplus.Entry{Mode:os.ModeDir|0755}
    appEntry, err := tree(*app); if err != nil { return err }; root.Children = append(root.Children, appEntry)
    for _, name := range []string{".DS_Store", ".background"} { node, e := tree(filepath.Join(*finder, name)); if e != nil { return e }; root.Children = append(root.Children, node) }
    root.Children = append(root.Children, &hfsplus.Entry{Name:"Applications", Mode:os.ModeSymlink|0755, Data:[]byte("/Applications")})
    raw, err := os.CreateTemp(filepath.Dir(*out), "rovunet-hfs-*.raw"); if err != nil { return err }
    defer os.Remove(raw.Name()); defer raw.Close()
    fixed := time.Date(2026,11,11,0,0,0,0,time.UTC)
    if err := hfsplus.CreateImage(raw,0,*volume,root,&hfsplus.CreateOptions{CaseInsensitive:true,FixedTime:fixed}); err != nil { return err }
    stat, err := raw.Stat(); if err != nil { return err }
    return disk.WrapRawImageDMGFrom(*out,raw,stat.Size(),"Apple_HFS",&disk.EncodeOptions{Compression:disk.CompressionZlib,Workers:4})
}
func main() { if err := run(); err != nil { fmt.Fprintln(os.Stderr,err);os.Exit(1) } }
