"""Inspect actual nupkg contents, not merely evaluated MSBuild properties."""
import sys
from pathlib import Path
import zipfile
import xml.etree.ElementTree as ET

expected = {'RedNb.Nacos', 'RedNb.Nacos.Http', 'RedNb.Nacos.Grpc',
            'RedNb.Nacos.DependencyInjection', 'RedNb.Nacos.AspNetCore', 'RedNb.Nacos.All'}
found = set()
for file in Path(sys.argv[1]).glob('*.nupkg'):
    with zipfile.ZipFile(file) as z:
        manifest = next(n for n in z.namelist() if n.endswith('.nuspec'))
        root = ET.fromstring(z.read(manifest))
        metadata = root.find('{*}metadata')
        name = metadata.findtext('{*}id')
        if name not in expected:
            continue
        found.add(name)
        assert metadata.findtext('{*}version') == '2.0.0', file
        assert metadata.findtext('{*}authors') == 'RedNb', file
        assert metadata.findtext('{*}license') == 'Apache-2.0', file
        assert 'README.md' in z.namelist(), file
        dlls = [n for n in z.namelist() if n.endswith('.dll')]
        if name == 'RedNb.Nacos.All':
            assert not dlls, 'Aggregate package must contain dependencies only'
        else:
            for tfm in ['net8.0', 'net10.0']:
                assert f'lib/{tfm}/{name}.dll' in dlls, (file, tfm)
                assert f'lib/{tfm}/{name}.xml' in z.namelist(), (file, tfm)
assert found == expected, f'Missing packages: {expected - found}'
print('All six package identities, metadata and contents verified.')
