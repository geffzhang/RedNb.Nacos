"""Require exactly the six reviewed release assets and verify GitHub's SHA-256 digests."""
import hashlib
import json
from pathlib import Path
import sys
import argparse
import re

parser = argparse.ArgumentParser()
parser.add_argument('release_json')
parser.add_argument('directory')
parser.add_argument('--version', required=True)
args = parser.parse_args()
assert re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?', args.version), 'Invalid package version'
release = json.loads(Path(args.release_json).read_text(encoding='utf-8-sig'))
directory = Path(args.directory)
ids = ['RedNb.Nacos', 'RedNb.Nacos.Http', 'RedNb.Nacos.Grpc',
       'RedNb.Nacos.DependencyInjection', 'RedNb.Nacos.AspNetCore', 'RedNb.Nacos.All']
expected = {name + '.' + args.version + '.nupkg' for name in ids}
assert release['tag_name'] == 'v' + args.version and not release['draft']
assert {p.name for p in directory.glob('*.nupkg')} == expected
assets = {a['name']: a for a in release['assets']}
assert {name for name in assets if name.endswith('.nupkg')} == expected, 'Unexpected release package assets'
for name in sorted(expected):
    digest = 'sha256:' + hashlib.sha256((directory / name).read_bytes()).hexdigest()
    assert assets[name].get('digest') == digest, f'Release asset hash mismatch: {name}'
print('Six release asset hashes verified.')
