//go:build unix

package ca

import (
	"os"
	"path/filepath"

	"golang.org/x/sys/unix"
)

// Android disallows app hard links. A directory flock serializes our writers;
// rename then publishes the complete certificate without replacing a first pin.
// Closing the directory releases the lock even if the process is killed.
func rovunetPublishFirst(temporary, path string) error {
	directory, err := os.Open(filepath.Dir(path))
	if err != nil {
		return err
	}
	defer directory.Close()
	for {
		err = unix.Flock(int(directory.Fd()), unix.LOCK_EX)
		if err != unix.EINTR {
			break
		}
	}
	if err != nil {
		return err
	}
	defer unix.Flock(int(directory.Fd()), unix.LOCK_UN)
	if _, err = os.Lstat(path); err == nil {
		return os.ErrExist
	} else if !os.IsNotExist(err) {
		return err
	}
	return os.Rename(temporary, path)
}
