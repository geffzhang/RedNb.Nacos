"""Require exactly the six reviewed release assets and verify GitHub's SHA-256 digests."""
import hashlib
import json
from pathlib import Path
import sys

release = json.loads(Path(sys.argv[1]).read_text(encoding='utf-8'))
directory = Path(sys.argv[2])
ids = ['RedNb.Nacos', 'RedNb.Nacos.Http', 'RedNb.Nacos.Grpc',
       'RedNb.Nacos.DependencyInjection', 'RedNb.Nacos.AspNetCore', 'RedNb.Nacos.All']
expected = {name + '.2.0.0.nupkg' for name in ids}
assert release['tag_name'] == 'v2.0.0' and not release['draft']
assert {p.name for p in directory.glob('*.nupkg')} == expected
assets = {a['name']: a for a in release['assets']}
for name in sorted(expected):
    digest = 'sha256:' + hashlib.sha256((directory / name).read_bytes()).hexdigest()
    assert assets[name].get('digest') == digest, f'Release asset hash mismatch: {name}'
print('Six release asset hashes verified.')
