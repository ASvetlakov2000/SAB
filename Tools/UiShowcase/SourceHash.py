"""Accept Git LF/CRLF conversion only; retain the immutable raw historical hashes."""
from pathlib import Path
import hashlib,json

ROOT=Path(__file__).resolve().parents[2]
CONTRACT=Path(__file__).with_name('source-line-endings.json')

def matches_bytes(path,data,expected):
    if hashlib.sha256(data).hexdigest()==expected:return True
    records=json.loads(CONTRACT.read_text(encoding='utf-8'))['files']
    record=records.get(Path(path).resolve().relative_to(ROOT).as_posix())
    return bool(record and record['rawSha256']==expected and hashlib.sha256(data.replace(b'\r\n',b'\n')).hexdigest()==record['lfSha256'])

def matches(path,expected):
    return matches_bytes(path,Path(path).read_bytes(),expected)

if __name__=='__main__':
    records=json.loads(CONTRACT.read_text(encoding='utf-8'))['files']
    for name,record in records.items():
        path=ROOT/name
        data=path.read_bytes().replace(b'\r\n',b'\n')
        assert matches_bytes(path,data,record['rawSha256']),name
        assert matches_bytes(path,data.replace(b'\n',b'\r\n'),record['rawSha256']),name
        assert not matches_bytes(path,data+b' altered',record['rawSha256']),name
    print('PASS source portability:',len(records),'files; LF/CRLF accepted; content mutation rejected')
