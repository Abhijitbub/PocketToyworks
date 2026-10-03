"""Import Unity's bundled TMP essential resources for a headless project.

Unity's interactive ImportPackage queues work and cannot finish inside a batch
executeMethod. Preserve original GUIDs and import only Assets/TextMesh Pro.
"""
import pathlib
import sys
import tarfile

project = pathlib.Path(sys.argv[1]).resolve()
packages = list((project / 'Library/PackageCache').glob('com.unity.ugui@*/Package Resources/TMP Essential Resources.unitypackage'))
if len(packages) != 1:
    raise RuntimeError('Expected one installed Unity UI package')
with tarfile.open(packages[0], 'r:gz') as archive:
    members = {m.name: m for m in archive.getmembers()}
    for name, member in members.items():
        if not name.endswith('/pathname'):
            continue
        relative = archive.extractfile(member).read().decode('utf-8').strip()
        if relative != 'Assets/TextMesh Pro' and not relative.startswith('Assets/TextMesh Pro/'):
            raise RuntimeError('Unexpected bundled asset path: ' + relative)
        target = (project / relative).resolve()
        if not target.is_relative_to(project / 'Assets'):
            raise RuntimeError('Asset escapes project')
        group = name.rsplit('/', 1)[0]
        asset = members.get(group + '/asset')
        if asset:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(archive.extractfile(asset).read())
        else:
            target.mkdir(parents=True, exist_ok=True)
        meta = members.get(group + '/asset.meta')
        if meta:
            pathlib.Path(str(target) + '.meta').write_bytes(archive.extractfile(meta).read())
print('Imported bundled TMP essential assets with original GUIDs')
