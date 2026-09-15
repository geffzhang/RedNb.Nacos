"""Inspect actual nupkg contents, not merely evaluated MSBuild properties."""
import sys
import argparse
import re
from pathlib import Path
import zipfile
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser()
parser.add_argument('directory')
parser.add_argument('--version', required=True)
args = parser.parse_args()
assert re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?', args.version), 'Invalid version'
expected = {'RedNb.Nacos', 'RedNb.Nacos.Http', 'RedNb.Nacos.Grpc',
            'RedNb.Nacos.DependencyInjection', 'RedNb.Nacos.AspNetCore', 'RedNb.Nacos.All'}
found = set()
for file in Path(args.directory).glob('*.nupkg'):
    with zipfile.ZipFile(file) as z:
        manifest = next(n for n in z.namelist() if n.endswith('.nuspec'))
        root = ET.fromstring(z.read(manifest))
        metadata = root.find('{*}metadata')
        name = metadata.findtext('{*}id')
        if name not in expected:
            raise AssertionError(f'Unexpected package: {name}')
        assert name not in found, f'Duplicate package identity: {name}'
        assert file.name == f'{name}.{args.version}.nupkg', file
        found.add(name)
        assert metadata.findtext('{*}version') == args.version, file
        assert metadata.findtext('{*}authors') == 'RedNb', file
        assert metadata.findtext('{*}license') == 'Apache-2.0', file
        assert 'README.md' in z.namelist(), file
        for dependency in metadata.findall('.//{*}dependency'):
            if dependency.get('id', '').startswith('RedNb.Nacos'):
                assert dependency.get('version', '').strip('[]') == args.version, (file, dependency.attrib)
        dlls = [n for n in z.namelist() if n.endswith('.dll')]
        if name == 'RedNb.Nacos.All':
            assert not dlls, 'Aggregate package must contain dependencies only'
        else:
            for tfm in ['net8.0', 'net10.0']:
                assert f'lib/{tfm}/{name}.dll' in dlls, (file, tfm)
                assert f'lib/{tfm}/{name}.xml' in z.namelist(), (file, tfm)
assert found == expected, f'Missing packages: {expected - found}'
print('All six package identities, metadata and contents verified.')
