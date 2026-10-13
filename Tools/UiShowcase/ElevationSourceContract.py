"""Immutable UI baseline plus the explicitly requested category and result-notification functional deltas."""
from pathlib import Path
import json, hashlib
from SourceHash import matches

def verify(root):
    baseline = json.loads((root/'Tools/UiShowcase/elevations-baseline.json').read_text(encoding='utf-8'))
    delta = json.loads((root/'Tools/UiShowcase/elevations-functional-delta.json').read_text(encoding='utf-8'))
    migration_path = root/'Tools/UiShowcase/elevations-production-migration.json'
    migration = json.loads(migration_path.read_text(encoding='utf-8')) if migration_path.exists() else {'files':{}}
    for name, before in baseline['files'].items():
        permitted = delta['files'].get(name)
        if permitted:
            assert permitted['before'] == before, 'Functional delta must retain original baseline: '+name
        expected = permitted['after'] if permitted else before
        if name in migration['files']:
            assert migration['files'][name]['before'] == expected, 'Migration must follow preserved functional delta: '+name
            expected = migration['files'][name]['after']
        assert matches(root/name, expected), 'Unexpected production change: '+name
    assert len(delta['files']) == 8 and set(delta['files']) <= set(baseline['files'])
    for name, change in migration['files'].items():
        assert matches(root/name, change['after']), 'Unexpected migrated source change: '+name
    return len(set(baseline['files']) - set(delta['files']) - set(migration['files'])), delta
