import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("check_docs", Path(__file__).parents[1] / "tools/check_docs.py")
docs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(docs)


class DocumentationTests(unittest.TestCase):
    def test_entry_points_and_current_sdk_follow_compiled_metadata(self):
        index = {"runtimeVersion": "1.30.0", "types": [
            {"name": "JKRuntime.UI.UIApi", "staticMembers": ["Open", "CreateMenuPage"]}]}
        self.assertEqual(docs.check_api_mentions("Current SDK: **1.30**; `UIApi.Open`", index), [])
        self.assertEqual(len(docs.check_api_mentions("Current SDK: **1.15**; `UIApi.OpenModal`", index)), 2)

    def test_headings_and_fenced_examples(self):
        text = '# A `call()`\n## Same\n## Same\n```md\n# Hidden\n[x](missing.md)\n```\n[x](real.md#same)'
        self.assertEqual(docs.anchors(text), {"a-call", "same", "same-1"})
        self.assertEqual(list(docs.links(text)), ["real.md#same"])

    def test_reference_links_and_titles(self):
        self.assertEqual(list(docs.links('[a](<file name.md#title> "title")\n[b]: file.md#anchor')), ['file name.md#title', 'file.md#anchor'])

    def test_generic_type_heading_retains_parameter_inside_code(self):
        self.assertEqual(docs.anchors('### `Example.Setting<T>`'), {'examplesettingt'})

    def test_detached_sdk_rejects_broken_and_external_local_links(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sdk = root / 'SDK'
            sdk.mkdir()
            (root / 'outside.md').write_text('# Outside')
            entry = sdk / 'README.md'
            entry.write_text('[outside](../outside.md)\n[absent](gone.md)\n[heading](page.md#absent)')
            (sdk / 'page.md').write_text('# Present')
            errors = docs.check_links(sdk, [entry], portable=True)
            self.assertEqual(len(errors), 3)
            self.assertTrue(any('escapes SDK' in e for e in errors))
            entry.write_text('[ok](page.md#present)\n[web](https://example.com)')
            self.assertEqual(docs.check_links(sdk, [entry], portable=True), [])

    def test_new_public_namespace_requires_guide(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'docs').mkdir()
            (root / 'docs/api-guides.json').write_text('{}')
            with self.assertRaisesRegex(ValueError, 'Public namespaces need guide routes'):
                docs.render_api(root, root, {'types': [{'ns': 'New.Service'}]})


if __name__ == '__main__':
    unittest.main()
