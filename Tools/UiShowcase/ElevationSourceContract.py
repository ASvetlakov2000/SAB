"""Immutable UI baseline plus the explicitly requested category and result-notification functional deltas."""
from pathlib import Path
import json, hashlib
from SourceHash import matches

def verify(root):
    baseline = json.loads((root/'Tools/UiShowcase/elevations-baseline.json').read_text(encoding='utf-8'))
    delta = json.loads((root/'Tools/UiShowcase/elevations-functional-delta.json').read_text(encoding='utf-8'))
    migration_path = root/'Tools/UiShowcase/elevations-production-migration.json'
    migration = json.loads(migration_path.read_text(encoding='utf-8')) if migration_path.exists() else {'files':{}}
    hotfix_path = root/'Tools/UiShowcase/elevations-hotfix-delta.json'
    hotfix = json.loads(hotfix_path.read_text(encoding='utf-8')) if hotfix_path.exists() else {'files':{}}
    parameter_path = root/'Tools/UiShowcase/parameter-check-delta.json'
    parameter = json.loads(parameter_path.read_text(encoding='utf-8')) if parameter_path.exists() else {'files':{}}
    def latest(name, expected):
        if name in parameter['files']:
            assert parameter['files'][name]['before'] == expected, 'Parameter fix must follow preserved source chain: '+name
            return parameter['files'][name]['after']
        return expected
    for name, before in baseline['files'].items():
        permitted = delta['files'].get(name)
        if permitted:
            assert permitted['before'] == before, 'Functional delta must retain original baseline: '+name
        expected = permitted['after'] if permitted else before
        if name in migration['files']:
            assert migration['files'][name]['before'] == expected, 'Migration must follow preserved functional delta: '+name
            expected = migration['files'][name]['after']
        if name in hotfix['files']:
            assert hotfix['files'][name]['before'] == expected, 'Hotfix must follow preserved migration: '+name
            expected = hotfix['files'][name]['after']
        assert matches(root/name, latest(name, expected)), 'Unexpected production change: '+name
    assert len(delta['files']) == 8 and set(delta['files']) <= set(baseline['files'])
    for name, change in migration['files'].items():
        expected = hotfix['files'].get(name, change)['after']
        if name in hotfix['files']: assert hotfix['files'][name]['before'] == change['after'], 'Hotfix migration chain differs: '+name
        assert matches(root/name, latest(name, expected)), 'Unexpected migrated source change: '+name
    parameter_path = root/'Tools/UiShowcase/parameter-check-delta.json'
    parameter = json.loads(parameter_path.read_text(encoding='utf-8')) if parameter_path.exists() else {'files':{}}
    for name, change in hotfix['files'].items():
        expected = change['after']
        if name in parameter['files']:
            assert parameter['files'][name]['before'] == expected, 'Parameter fix must follow preserved hotfix: '+name
            expected = parameter['files'][name]['after']
        assert matches(root/name, expected), 'Unexpected hotfix source change: '+name
    return len(set(baseline['files']) - set(delta['files']) - set(migration['files'])), delta
