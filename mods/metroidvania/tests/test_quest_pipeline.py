import copy
import json
import sys
import tempfile
import struct
import zlib
import unittest
from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image

sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'build-support'))
from quest_manifest import DEFAULT_MANIFEST,load_manifest,manifest_hash
from compile_quest_content import compile_catalog,verify_assets
from quest_source import sound_recipe,validate_clip_events


class PipelineTests(unittest.TestCase):
    def setUp(self):
        self.manifest=load_manifest()
        scratch=Path(__file__).resolve().parents[3]/'build/_work/metroidvania/infrastructure'
        scratch.mkdir(parents=True,exist_ok=True)
        self.folder=tempfile.TemporaryDirectory(dir=scratch)
        self.root=Path(self.folder.name)

    def tearDown(self):self.folder.cleanup()

    def read(self,data):
        path=self.root/'manifest.json';path.write_text(json.dumps(data));return load_manifest(path)

    def models(self,manifest):
        for recipe in manifest['enemies']:
            folder=self.root/'models'/recipe['id'];folder.mkdir(parents=True,exist_ok=True)
            cue={'id':'jkq.11111111-2222-3333-4444-555555555555','event_guid':'11111111-2222-3333-4444-555555555555','tick':8,'stoppable':False}
            data={'id':recipe['id'],'health':200,'behavior':recipe['behavior'],'adaptation':recipe['adaptation'],'render':recipe['render'],'clips':[{'sounds':[]},{'sounds':[cue]},{'sounds':[cue]},{'sounds':[cue]}], 'hit_sounds':[cue], 'attacks':[{'id':'source-attack','clip':4,'weight':100,'active_tick':12,'damage':32,'sounds':[cue]}]}
            (folder/'enemy.json').write_text(json.dumps(data));(folder/'source-receipt.json').write_text(json.dumps({'manifest_sha256':manifest_hash(manifest)}))
        return self.root/'models'

    def test_add_an_enemy_without_code_or_audio_list_edits(self):
        data=copy.deepcopy(self.manifest);second=copy.deepcopy(data['enemies'][0]);second['id']='another-melee';second['adaptation']['body_width']=24;data['enemies'].append(second);data=self.read(data)
        result=compile_catalog(data,self.models(data),self.root/'out')
        self.assertEqual([node.attrib['Id'] for node in result.findall('Enemies/Enemy')],['fslave-crawler','another-melee'])
        self.assertEqual(result.findall('Enemies/Enemy')[1].attrib['BodyWidth'],'24')
        self.assertEqual(len(result.findall('Audio/Cue')),len(data['audio'])+1)
        self.assertTrue(any(node.attrib['Path']=='enemies/another-melee.bin' for node in result.findall('Assets/Asset')))
        self.assertEqual(len(result.findall('Enemies/Enemy')[0].findall('HurtSounds/Sound')),1)
        self.assertEqual(len(result.findall('Enemies/Enemy')[0].findall('DeathSounds/Sound')),1)

    def test_manifest_typos_duplicate_ids_and_unknown_profiles(self):
        for edit in [lambda d:d['enemies'].append(copy.deepcopy(d['enemies'][0])),lambda d:d['enemies'][0].update(behavior='flying-boss'),lambda d:d['enemies'][0]['adaptation'].update(damgae=5),lambda d:d['items'][0].update(light_cue='missing'),lambda d:d['items'][1].update(visual_id=1),lambda d:d['enemies'][0]['render'].update(fps=7),lambda d:d['enemies'][0]['render'].update(columns=1.5)]:
            data=copy.deepcopy(self.manifest);edit(data)
            with self.assertRaises(ValueError):self.read(data)

    def test_duplicate_json_fields_and_source_paths(self):
        path=self.root/'bad.json';path.write_text('{"version":1,"version":2}')
        with self.assertRaises(ValueError):load_manifest(path)
        data=copy.deepcopy(self.manifest);data['enemies'][0]['id']='../escape'
        with self.assertRaises(ValueError):self.read(data)

    def test_source_markers_convert_source_cadence_and_keep_guid(self):
        sound={'type':0,'startFrame':8,'isStoppable':1,'eventRef':{'Guid':{'Data1':1,'Data2':2,'Data3':3,'Data4':4}}}
        cue=sound_recipe(sound,60,30);self.assertEqual(cue['tick'],16);self.assertTrue(cue['stoppable']);self.assertTrue(cue['id'].startswith('jkq.'))
        sound['type']=99
        with self.assertRaises(ValueError):sound_recipe(sound,60,30)
        validate_clip_events([{'functionName':'activeStart'}])
        with self.assertRaisesRegex(ValueError,'explicit adapter'):validate_clip_events([{'functionName':'spawnProjectile'}])

    def test_stale_extraction_is_refused(self):
        models=self.models(self.manifest);self.manifest['enemies'][0]['adaptation']['damage_scale']=9
        with self.assertRaisesRegex(ValueError,'Stale'):compile_catalog(self.manifest,models,self.root/'out')

    def test_missing_assets_and_escaped_output_are_refused(self):
        result=compile_catalog(self.manifest,self.models(self.manifest),self.root/'out')
        with self.assertRaisesRegex(ValueError,'Missing'):verify_assets(result,self.root/'out')
        result.find('Assets/Asset').set('Path','../escape')
        with self.assertRaisesRegex(ValueError,'escaped'):verify_assets(result,self.root/'out')

    def test_weapon_timeline_override_and_saved_ids_are_compiled(self):
        self.manifest['items'][0]['attacks']={'light':{'start':6,'active':5}}
        result=compile_catalog(self.manifest,self.models(self.manifest),self.root/'out');item=result.find('Items/Item')
        self.assertEqual(item.attrib['Id'],'worn-blade');self.assertEqual(item.attrib['LightStart'],'6');self.assertEqual(item.attrib['LightActive'],'5');self.assertEqual(item.attrib['VisualId'],'1')

    def test_unfinished_png_stream_is_refused_even_with_valid_chunk_crcs(self):
        def chunk(kind,data):return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data))
        path=self.root/'atlas.png';raw=zlib.compress(b'\x00'+b'\x10\x20\x30\xff'*16)
        path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',16,1,8,6,0,0,0))+chunk(b'IDAT',raw[:len(raw)//2])+chunk(b'IEND',b''))
        root=ET.Element('Content');assets=ET.SubElement(root,'Assets');ET.SubElement(assets,'Asset',Path='atlas.png',Resource='fixture.atlas')
        with self.assertRaisesRegex(ValueError,'Invalid compiled PNG'):verify_assets(root,self.root)
        Image.new('RGBA',(16,1),(16,32,48,255)).save(path)
        verify_assets(root,self.root);self.assertIn('SHA256',assets[0].attrib)


if __name__=='__main__':unittest.main()
