"""Compile validated recipes and extracted source facts into the runtime catalogue."""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image
from quest_manifest import load_manifest,DEFAULT_MANIFEST,manifest_hash


def compile_catalog(manifest,models,output):
    output=Path(output);output.mkdir(parents=True,exist_ok=True)
    enemies=[]
    for entry in manifest['enemies']:
        folder=Path(models)/entry['id'];receipt=json.loads((folder/'source-receipt.json').read_text(encoding='utf-8-sig'))
        if receipt['manifest_sha256']!=manifest_hash(manifest):raise ValueError('Stale extracted enemy data: '+entry['id'])
        enemy=json.loads((folder/'enemy.json').read_text())
        if enemy['id']!=entry['id'] or enemy['behavior']!=entry['behavior']:raise ValueError('Extracted enemy identity mismatch')
        enemies.append(enemy)
    cues=[dict(c) for c in manifest['audio']];known={c['id'] for c in cues}
    for enemy in enemies:
        lists=[c['sounds'] for c in enemy['clips']]+[enemy['hit_sounds']]
        for sounds in lists:
            for sound in sounds:
                if sound['id'] not in known:cues.append({k:v for k,v in sound.items() if k in ('id','event_guid')});known.add(sound['id'])
    if len(cues)>64:raise ValueError('Selected content exceeds the 64-cue budget; split the content pack')
    root=ET.Element('Content',Version='1',ManifestHash=manifest_hash(manifest),DefaultEnemy=manifest['default_enemy'])
    ET.SubElement(root,'PlayerAttack',{k:str(v) for k,v in manifest['player_attack'].items()})
    audio=ET.SubElement(root,'Audio')
    for index,cue in enumerate(cues):ET.SubElement(audio,'Cue',Id=cue['id'],Index=str(index),Resource='Metroidvania.Audio.'+cue['id'])
    items=ET.SubElement(root,'Items')
    for index,item in enumerate(manifest['items']):
        attrs={key:str(item.get(name,'')) for key,name in [('Id','id'),('Name','name'),('Description','description'),('Slot','slot'),('Damage','damage'),('Reach','reach'),('Armor','armor'),('LightCue','light_cue'),('HeavyCue','heavy_cue'),('HitCue','hit_cue')]}
        attrs.update(Index=str(index),ArtIndex=str(item['art']['index']),ArtKind=item['art']['kind'],VisualId=str(item.get('visual_id',0)),Heal=str(item.get('heal',0)))
        base=manifest['player_attack'];light=item.get('attacks',{}).get('light',{});heavy=item.get('attacks',{}).get('heavy',{})
        attrs.update(LightStart=str(light.get('start',base['light_start'])),LightActive=str(light.get('active',base['light_active'])),HeavyStart=str(heavy.get('start',base['heavy_start'])),HeavyActive=str(heavy.get('active',base['heavy_active'])),HeavyRecovery=str(heavy.get('recovery_extra',base['heavy_recovery_extra'])),HeavyMultiplier=str(heavy.get('multiplier',base['heavy_multiplier'])))
        ET.SubElement(items,'Item',attrs)
    definitions=ET.SubElement(root,'Enemies')
    def sounds(parent,name,entries):
        group=ET.SubElement(parent,name)
        counts={}
        if len(entries)>64:raise ValueError('Sound timeline exceeds the marker budget')
        for cue in entries:
            counts[cue['tick']]=counts.get(cue['tick'],0)+1
            if counts[cue['tick']]>4:raise ValueError('Sound timeline exceeds the per-tick event budget')
            ET.SubElement(group,'Sound',Cue=cue['id'],Tick=str(cue['tick']),Stoppable=str(cue['stoppable']).lower())
    for index,enemy in enumerate(enemies,1):
        a=enemy['adaptation'];r=enemy['render'];identifier=enemy['id']
        recipe=next(entry for entry in manifest['enemies'] if entry['id']==identifier)
        attrs={'Id':identifier,'Name':recipe['name'],'Index':str(index),'Behavior':enemy['behavior'],'Health':str(enemy['health']),'Data':'Metroidvania.Enemy.'+identifier+'.data','Atlas':'Metroidvania.Enemy.'+identifier+'.atlas'}
        attrs.update({k:str(a[n]) for k,n in [('BodyWidth','body_width'),('BodyHeight','body_height'),('AnchorX','anchor_x'),('AnchorY','anchor_y'),('WalkSpeed','walk_speed'),('AttackRange','attack_range'),('HeightRange','height_range'),('Cooldown','attack_cooldown'),('HurtTicks','hurt_ticks'),('DeathTicks','death_ticks'),('DeathHoldTick','death_hold_tick')]})
        attrs.update({k:str(r[n]) for k,n in [('Cell','cell'),('Columns','columns'),('PivotX','pivot_x'),('PivotY','pivot_y')]})
        node=ET.SubElement(definitions,'Enemy',attrs)
        for attack in enemy['attacks']:
            action=ET.SubElement(node,'Attack',Id=attack['id'],Clip=str(attack['clip']),Weight=str(attack['weight']),ActiveTick=str(attack['active_tick']),Damage=str(attack['damage']))
            sounds(action,'Sounds',attack['sounds'])
        hurt={}
        for cue in enemy['hit_sounds']+enemy['clips'][2]['sounds']:hurt[(cue['id'],cue['tick'])]=cue
        sounds(node,'WalkSounds',enemy['clips'][1]['sounds']);sounds(node,'IdleSounds',enemy['clips'][0]['sounds']);sounds(node,'HurtSounds',list(hurt.values()));sounds(node,'DeathSounds',enemy['clips'][3]['sounds'])
    assets=ET.SubElement(root,'Assets')
    for cue in cues:ET.SubElement(assets,'Asset',Path='audio/'+cue['id']+'.wav',Resource='Metroidvania.Audio.'+cue['id'])
    for enemy in enemies:
        identifier=enemy['id']
        ET.SubElement(assets,'Asset',Path='enemies/'+identifier+'.bin',Resource='Metroidvania.Enemy.'+identifier+'.data')
        ET.SubElement(assets,'Asset',Path='enemies/'+identifier+'.png',Resource='Metroidvania.Enemy.'+identifier+'.atlas')
    for name in ('icons','weapons','armor'):ET.SubElement(assets,'Asset',Path='equipment/quest-'+name+'.png',Resource='Metroidvania.Art.'+name)
    ET.indent(root);ET.ElementTree(root).write(output/'catalog.xml',encoding='utf-8',xml_declaration=True)
    (output/'audio-recipe.json').write_text(json.dumps({'banks':manifest['banks'],'audio':cues},indent=2),encoding='utf-8')
    return root


def verify_assets(root,assets):
    seen=set()
    for entry in root.find('Assets'):
        relative=Path(entry.attrib['Path'])
        if relative.is_absolute() or '..' in relative.parts:raise ValueError('Asset escaped staging')
        if entry.attrib['Resource'] in seen:raise ValueError('Duplicate resource ID')
        seen.add(entry.attrib['Resource']);file=Path(assets)/relative
        if not file.is_file():raise ValueError('Missing compiled asset: '+str(relative))
        if file.suffix=='.png':
            # a valid PNG chunk/CRC list can still contain an unfinished deflate stream
            try:
                with Image.open(file) as image:
                    if image.format!='PNG' or image.width>8192 or image.height>8192:raise ValueError('Invalid atlas dimensions')
                    image.load()
            except (OSError,ValueError) as error:raise ValueError('Invalid compiled PNG: '+str(relative)) from error
        entry.set('SHA256',hashlib.sha256(file.read_bytes()).hexdigest())
    ET.indent(root);ET.ElementTree(root).write(Path(assets)/'catalog.xml',encoding='utf-8',xml_declaration=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--manifest',type=Path,default=DEFAULT_MANIFEST);parser.add_argument('--models',type=Path,required=True);parser.add_argument('--output',type=Path,required=True);parser.add_argument('--verify-assets',type=Path)
    args=parser.parse_args();root=compile_catalog(load_manifest(args.manifest),args.models,args.output);root.set('RecipeFileHash',hashlib.sha256(args.manifest.read_text(encoding='utf-8-sig').encode('utf-8')).hexdigest())
    if args.verify_assets:verify_assets(root,args.verify_assets)
    else:ET.indent(root);ET.ElementTree(root).write(args.output/'catalog.xml',encoding='utf-8',xml_declaration=True)
