"""Query the durable collision-colour registry, without loading game/mod DLLs."""
from __future__ import annotations
import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / 'docs/modding/block-registry/catalog.json'

def owners(rgb, data):
    r, g, b = rgb
    return [f for f in data['factories'] if any(r == a and g == c and lo <= b <= hi for a, c, lo, hi in f['rgb_ranges'])]

def validate(data):
    used = set()
    for reservation in data['reservations']:
        rgb = tuple(reservation['rgb'])
        assert all(0 <= channel <= 255 for channel in rgb)
        assert rgb not in used, f'Duplicate reservation: {rgb}'
        assert not owners(rgb, data), f'Reservation conflicts with installed blocks: {rgb}'
        used.add(rgb)
    return used

def import_labels(data):
    """Attach literal declaration names as lookup hints, not additional claims."""
    declaration = re.compile(r'\bColor\s+(\w*(?:Block|BLOCK|CODE|Code)\w*)\s*=\s*new\s+Color\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)(?:\s*,\s*255)?\s*\)')
    for factory in data['factories']:
        factory['labels'] = []
    for path in (ROOT / 'build/block-registry/decompiled').rglob('*.cs'):
        owner = path.relative_to(ROOT / 'build/block-registry/decompiled').parts[0].split('-')[0]
        for match in declaration.finditer(path.read_text(encoding='utf-8-sig')):
            rgb = [int(match[i]) for i in (2,3,4)]
            for factory in owners(rgb, data):
                if factory['owner'] == owner:
                    label = {'name': match[1], 'rgb': rgb, 'declaration': path.name}
                    if label not in factory['labels']:
                        factory['labels'].append(label)
    # Format numeric arrays on one line: a readable, diffable registry, not
    # 25,000 lines of single channel values. This is a mechanical data refresh.
    serialized = json.dumps(data, indent=2)
    serialized = re.sub(r'\[\s+(\d+),\s+(\d+),\s+(\d+)(?:,\s+(\d+))?\s+\]',
        lambda m: '[' + ', '.join(v for v in m.groups() if v is not None) + ']', serialized)
    CATALOG.write_text(serialized + '\n', encoding='utf-8')
    print(f"[OK] Imported {sum(len(f['labels']) for f in data['factories'])} verified-colour declaration labels")

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--rgb', type=int, nargs=3)
    parser.add_argument('--refresh-labels', action='store_true')
    args = parser.parse_args()
    data = json.loads(CATALOG.read_text(encoding='utf-8'))
    validate(data)
    if args.refresh_labels:
        import_labels(data)
    if args.rgb:
        matches = owners(args.rgb, data)
        for match in matches:
            print(f"{match['owner']}: {match['assembly']} / {match['factory']}")
            for label in match.get('labels', []):
                if label['rgb'] == args.rgb:
                    print(f"  {label['name']} ({label['declaration']})")
        for reserved in data['reservations']:
            if reserved['rgb'] == args.rgb:
                print(f"Reserved: {reserved['owner']} / {reserved['name']}")
        if not matches:
            print('No installed-factory collision in the audited snapshot.')
    else:
        print(f"[OK] {len(data['factories'])} factory/special-pixel entries; {len(data['reservations'])} non-conflicting reservations")
