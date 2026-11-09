"""Stage a software-only REVIEW source export. Never publishes, signs or licenses it."""
from pathlib import Path
import argparse
import hashlib
import json
import re
import zipfile

ROOT=Path(__file__).resolve().parents[1]
DERIVED={'driver/common.h','driver/vhidmini.c','driver/vhidmini.h','driver/util.c','driver/MinimalJoystick.inf'}
def sha(data):return hashlib.sha256(data).hexdigest()
def inventory():
    files={}
    for p in ROOT.glob('*'):
        if p.is_file() and (p.suffix in {'.cs','.ps1'} or p.name in {'LICENSE','LICENSING.md','HARDWARE-USB.md','BLUETOOTH-ARCHITECTURE.md','VALIDATION-0.3.md','VALIDATION-0.4.md'}):files[p.relative_to(ROOT).as_posix()]=p
    for folder in ['driver','vendor','tests','simulator-support','publication']:
        for p in (ROOT/folder).rglob('*'):
            if not p.is_file() or '__pycache__' in p.parts:continue
            if p.name in {'LICENSE-MIT.proposed.txt','SIGNPATH-ELIGIBILITY-INQUIRY.txt'}:continue
            if p.suffix in {'.c','.cpp','.h','.cs','.inf','.ps1','.py','.md','.txt','.json','.png','.joy','.csv','.rc','.inx','.vcxproj','.sln','.Filters'} or p.name=='LICENSE':
                files[p.relative_to(ROOT).as_posix()]=p
    for name in ['prepare_driver.py','prepare_public_source.py','build_support_art.py','InspectController.cs','InspectDirectInput.cpp','Bootstrap-OpenSource.ps1','UsbTestMain.cs']:
        p=ROOT/'tools'/name;files[p.relative_to(ROOT).as_posix()]=p
    return files
def check_upstream():
    provenance=json.loads((ROOT/'vendor/provenance.json').read_text())
    entries=json.loads((ROOT/'vendor/upstream-file-hashes.json').read_text())
    assert len(entries)==len(provenance['files']), 'Incomplete upstream evidence'
    results=[]
    for entry in entries:
        name=entry['path'].removeprefix('hid/vhidmini2/')
        data=(ROOT/'vendor/vhidmini2'/name).read_bytes()
        blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()
        assert blob==entry['sha'], 'Vendor drift: '+name
        results.append({'path':'vendor/vhidmini2/'+name,'git_blob_sha1':blob,'sha256':sha(data)})
    return results
def build(destination):
    assert not destination.exists(), 'Use a fresh destination; do not overwrite evidence'
    check_upstream();files=inventory();manifest=[]
    for rel,p in sorted(files.items()):
        assert not p.is_symlink(), rel
        assert not any(x in Path(rel).parts for x in ['build','.git','.devvm','.toolchain','__pycache__'])
        assert p.suffix.lower() not in {'.exe','.dll','.pfx','.p12','.key','.cer','.cat','.log','.iso'}
        data=p.read_bytes()
        if p.suffix not in {'.png'}:
            text=data.decode('utf-8-sig')
            assert not re.search(r'-----BEGIN (?:RSA |EC |ENCRYPTED )?PRIVATE KEY-----|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}',text), rel
        license_id='MS-PL' if rel.startswith('vendor/vhidmini2/') or rel in DERIVED or rel=='driver/LICENSE' else 'MIT'
        manifest.append({'path':'software/'+rel,'bytes':len(data),'sha256':sha(data),'source_license':license_id})
    destination.mkdir(parents=True)
    for rel,p in sorted(files.items()):
        out=destination/'software'/rel;out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(p.read_bytes())
    extras={'README.md':ROOT/'publication/PUBLIC-README.md','LICENSE':ROOT/'LICENSE','LICENSING.md':ROOT/'publication/REPOSITORY-LICENSING.md','.github/workflows/desktop-source-build.yml':ROOT.parent/'.github/workflows/desktop-source-build.yml'}
    for rel,p in extras.items():
        data=p.read_bytes();out=destination/rel;out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(data)
        manifest.append({'path':rel,'bytes':len(data),'sha256':sha(data),'source_license':'MIT'})
    generated={'.gitattributes':b'# Preserve byte-exact upstream sources and manifest hashes.\n* -text\n','PROTOTYPE-STATUS.md':b'# Development source snapshot\n\nUSB reception/calibration is implemented; physical COM, BLE, simulator and clean-Windows driver acceptance remain pending. This source snapshot is not a signed public product release.\n'}
    for rel,data in generated.items():
        (destination/rel).write_bytes(data)
        manifest.append({'path':rel,'bytes':len(data),'sha256':sha(data),'source_license':'MIT'})
    (destination/'SOURCE-MANIFEST.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    archive=destination.with_suffix('.zip')
    assert not archive.exists()
    with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        for p in sorted(destination.rglob('*')):
            if p.is_file():
                info=zipfile.ZipInfo(p.relative_to(destination).as_posix(),(2026,1,1,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED;info.external_attr=0o100644<<16;z.writestr(info,p.read_bytes())
    print(json.dumps({'files':len(manifest),'archive':str(archive),'archive_sha256':sha(archive.read_bytes()),'status':'REVIEW ONLY; nothing published'},indent=2))
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('destination',type=Path);build(parser.parse_args().destination.resolve())
