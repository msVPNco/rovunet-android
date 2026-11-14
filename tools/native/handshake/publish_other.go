//go:build !unix

package ca

import "os"

func rovunetPublishFirst(temporary, path string) error {
	return os.Link(temporary, path)
}
