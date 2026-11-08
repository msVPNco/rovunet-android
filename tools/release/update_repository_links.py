#!/usr/bin/env python3
"""Update public release navigation after the owner-requested repository rename.

Bodies only: never replace APKs, source/provenance/checksum assets, tags or Latest.
"""
import json
import os
import subprocess

REPO = 'msVPNco/rovunet-app'
OLD = 'msVPNco/rovunet-android'


def api(path, payload=None):
    command = ['gh', 'api', 'repos/' + REPO + path]
    if payload is not None:
        command += ['--method', 'PATCH', '--input', '-']
    return json.loads(subprocess.check_output(command, input=json.dumps(payload).encode() if payload is not None else None))


def identity(release):
    return (release['id'], release['tag_name'], release['draft'], release['prerelease'],
            release['target_commitish'], sorted((a['id'], a['name'], a['size'], a.get('digest')) for a in release['assets']))


def main():
    if os.environ.get('GITHUB_REPOSITORY') != REPO or os.environ.get('GITHUB_REF') != 'refs/heads/maintenance/canonical-repository-links-20261108':
        raise RuntimeError('Not the owner-authorized repository rename trigger')
    if api('')['id'] != 1445854036:
        raise RuntimeError('Repository identity changed')
    latest_id = api('/releases/latest')['id']
    changes = 0
    for page in range(1, 11):
        releases = api(f'/releases?per_page=100&page={page}')
        for release in releases:
            if release['draft']:
                continue
            body = release.get('body') or ''
            if OLD not in body:
                continue
            replacement = body.replace(OLD, REPO)
            updated = api('/releases/' + str(release['id']), {'body': replacement})
            if updated['body'] != replacement or identity(updated) != identity(release):
                raise RuntimeError('Unexpected change outside release navigation')
            changes += 1
            print('Updated navigation only:', release['tag_name'], flush=True)
        if len(releases) < 100:
            break
    else:
        raise RuntimeError('Release listing exceeded the bounded scan')
    if api('/releases/latest')['id'] != latest_id:
        raise RuntimeError('Latest changed during metadata update')
    print(f'Updated {changes} public release bodies; APKs, assets, tags and Latest unchanged.', flush=True)


if __name__ == '__main__':
    main()
