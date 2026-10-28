"""Validate the maintained import recipes before opening Quest assets."""
import hashlib
import json
import math
import re
from pathlib import Path

DEFAULT_MANIFEST=Path(__file__).resolve().parents[1]/'content/jkq-import.json'


def identifier(value):
    if not isinstance(value,str) or not re.fullmatch(r'[a-z0-9][a-z0-9.-]{0,63}',value):
        raise ValueError('Invalid content ID: '+repr(value))
    return value


def number(value,low,high,name):
    if isinstance(value,bool) or not isinstance(value,(int,float)) or not math.isfinite(value) or not low<=value<=high:
        raise ValueError('Invalid '+name+': '+repr(value))
    return value


def integer(value,low,high,name):
    number(value,low,high,name)
    if not isinstance(value,int):raise ValueError('Expected integer '+name)


def fields(value,allowed,kind):
    unknown=set(value)-set(allowed.split())
    if unknown:raise ValueError('Unknown '+kind+' field: '+','.join(sorted(unknown)))


def load_manifest(path=DEFAULT_MANIFEST):
    def unique_pairs(pairs):
        result={}
        for key,value in pairs:
            if key in result:raise ValueError('Duplicate JSON key: '+key)
            result[key]=value
        return result
    data=json.loads(Path(path).read_text(encoding='utf-8-sig'),object_pairs_hook=unique_pairs)
    if data.get('version')!=1 or data.get('simulation_hz')!=60:raise ValueError('Unsupported import manifest')
    if set(data)-{'version','unity_version','simulation_hz','default_enemy','banks','audio','items','enemies','player_attack'}:raise ValueError('Unknown manifest field')
    for group,maximum in [('items',128),('enemies',32),('audio',64)]:
        values=data[group]
        if not isinstance(values,list) or not 1<=len(values)<=maximum:raise ValueError('Invalid '+group+' count')
        seen=set()
        for entry in values:
            key=identifier(entry['id'])
            if key in seen:raise ValueError('Duplicate '+group+' ID: '+key)
            seen.add(key)
    for enemy in data['enemies']:
        if set(enemy)-{'id','name','source_prefab','source_brain','behavior','states','render','adaptation','notes'}:raise ValueError('Unknown enemy recipe field: '+enemy['id'])
        if not isinstance(enemy.get('name'),str) or not 1<=len(enemy['name'])<=64:raise ValueError('Missing enemy display name')
        if enemy['behavior']!='grounded-melee':raise ValueError('Unsupported behavior: '+enemy['behavior'])
        if not enemy['source_prefab'].endswith('.prefab') or '..' in enemy['source_prefab']:raise ValueError('Invalid source prefab')
        r=enemy['render'];a=enemy['adaptation']
        fields(r,'fps pixels_per_unit cell columns pivot_x pivot_y center_x center_y extent','render')
        fields(a,'body_width body_height anchor_x anchor_y damage_scale walk_speed attack_range height_range attack_cooldown hurt_ticks death_ticks death_hold_tick','adaptation')
        integer(r['fps'],5,60,'render fps')
        if 60%r['fps']:raise ValueError('Render fps must divide simulation rate')
        for name,low,high in [('cell',16,128),('columns',1,32)]:integer(r[name],low,high,name)
        for name,low,high in [('pixels_per_unit',1,32),('extent',1,32),('center_x',-32,32),('center_y',-32,32)]:number(r[name],low,high,name)
        for name in ('pivot_x','pivot_y'):integer(r[name],0,r['cell'],name)
        for name in ('body_width','body_height'):integer(a[name],1,128,name)
        for name in ('anchor_x','anchor_y'):integer(a[name],-128,128,name)
        for name,low,high in [('damage_scale',.01,1000),('walk_speed',0,4)]:number(a[name],low,high,name)
        for name,low,high in [('attack_range',1,256),('height_range',0,128),('attack_cooldown',0,600),('hurt_ticks',1,600),('death_ticks',1,600),('death_hold_tick',0,600)]:integer(a[name],low,high,name)
        if a['death_hold_tick']>a['death_ticks']:raise ValueError('Death hold is after its lifetime')
        if set(enemy['states'])!={'idle','walk','hurt','death'}:raise ValueError('Missing animation state recipe')
    if data['default_enemy'] not in {e['id'] for e in data['enemies']}:raise ValueError('Missing default enemy')
    audio={e['id'] for e in data['audio']}
    for cue in data['audio']:
        fields(cue,'id event','audio')
        if not cue['event'].startswith('event:/'):raise ValueError('Invalid FMOD event')
        if cue['id'].startswith('jkq.'):raise ValueError('jkq. is reserved for discovered source sounds')
    visual_ids=set();art_ids=set()
    for item in data['items']:
        fields(item,'id source source_sprite name description slot damage reach armor light_cue heavy_cue hit_cue art visual_id heal attacks','item')
        if item['slot'] not in ('None','Weapon','Shield','Body'):raise ValueError('Unknown item slot')
        for field,high in [('damage',1000),('reach',128),('armor',200)]:integer(item[field],0,high,field)
        integer(item['visual_id'],0,255,'visual_id');integer(item.get('heal',0),0,2000,'heal')
        if item['slot']!='None':
            pair=(item['slot'],item['visual_id'])
            if not item['visual_id'] or pair in visual_ids:raise ValueError('Duplicate appearance token')
            visual_ids.add(pair)
        art=item['art'];fields(art,'kind index view height gain','item art');integer(art['index'],0,127,'art index')
        if art['kind'] not in ('weapon','shield','body','consumable'):raise ValueError('Unsupported equipment art recipe')
        if art['kind']!='consumable':
            pair=('held' if art['kind'] in ('weapon','shield') else 'body',art['index'])
            if pair in art_ids:raise ValueError('Duplicate atlas slot')
            art_ids.add(pair);integer(art['view'],0,3,'item view')
        if art['kind'] in ('weapon','shield'):integer(art['height'],1,60,'held height')
        if art['kind']=='body':number(art['gain'],.1,4,'armor gain')
        for kind,attack in item.get('attacks',{}).items():
            if kind not in ('light','heavy'):raise ValueError('Unknown weapon action')
            fields(attack,'start active' if kind=='light' else 'start active recovery_extra multiplier','weapon action')
            for name,value in attack.items():integer(value,1,8 if name=='multiplier' else 120,name)
        if item['slot']=='Weapon':
            if item['damage']<1 or item['reach']<1:raise ValueError('Weapon damage and reach must be positive')
            if not all(name in item for name in ('light_cue','heavy_cue','hit_cue')):raise ValueError('Weapon sound roles must be explicit')
        for field in ('light_cue','heavy_cue','hit_cue'):
            if field in item and item[field] not in audio:raise ValueError('Missing item cue: '+item[field])
        if ('source' in item)==('source_sprite' in item):raise ValueError('Choose one item source')
    for name in ('light_start','light_active','heavy_start','heavy_active','heavy_recovery_extra','heavy_multiplier'):integer(data['player_attack'][name],1,8 if name=='heavy_multiplier' else 120,name)
    return data


def manifest_hash(data):
    return hashlib.sha256(json.dumps(data,sort_keys=True,separators=(',',':')).encode()).hexdigest()
