"""Bake selected Quest skeleton animations into authoring meshes, without Unity."""
import argparse
import hashlib
import json
import math
import struct
import zlib
from pathlib import Path
import numpy as np
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
from quest_animation import Clip, transform
from quest_manifest import load_manifest,DEFAULT_MANIFEST,manifest_hash
from quest_source import QuestSource,sound_recipe,validate_clip_events


def import_enemy(source,manifest,recipe,out):
    out.mkdir(parents=True, exist_ok=True)
    prefab,states,attacks,source_colliders,health,animations=source.enemy(recipe)
    resolve=source.resolve
    render=recipe['render'];fps=render['fps'];ppu=render['pixels_per_unit']
    collider_ids={obj.path_id:kind for kind,values in source_colliders.items() for obj in values}
    def vector(value, keys='xyz'):
        return np.asarray([value[k] for k in keys], dtype=float)
    nodes, renderers, by_id, paths, colliders = [], [], {}, {}, []
    def walk(go, parent, path):
        data = go.parse_as_dict()
        components = [resolve(go, entry['component']) for entry in data['m_Component']]
        tr = next(c for c in components if c.type.name == 'Transform')
        tree = tr.parse_as_dict()
        index = len(nodes)
        by_id[tr.path_id] = index
        path_hash = zlib.crc32(path.encode()) if path else 0
        if path_hash in paths:
            raise ValueError('Duplicate skeleton path')
        paths[path_hash] = index
        nodes.append((parent, path_hash, vector(tree['m_LocalPosition']), vector(tree['m_LocalRotation'], 'xyzw'), vector(tree['m_LocalScale'])))
        for component in components:
            if component.type.name == 'SkinnedMeshRenderer':
                renderers.append((component, component.parse_as_dict(),None))
            if component.type.name == 'MeshFilter':
                renderer=next((c for c in components if c.type.name=='MeshRenderer'),None)
                if renderer is not None:
                    render_data=source.data(renderer).copy();render_data['m_Mesh']=source.data(component)['m_Mesh'];renderers.append((renderer,render_data,index))
            if component.type.name == 'BoxCollider2D' and component.path_id in collider_ids:
                colliders.append((collider_ids[component.path_id],index,component.parse_as_dict()))
        for child in tree['m_Children']:
            child_tr = resolve(tr, child)
            child_go = resolve(child_tr, child_tr.parse_as_dict()['m_GameObject'])
            walk(child_go, index, (path+'/' if path else '')+child_go.parse_as_dict()['m_Name'])
    def find_animator(go):
        components=source.components(go)
        if any(c.type.name=='Animator' for c in components):return go
        tr=next(c for c in components if c.type.name=='Transform')
        for child in source.data(tr)['m_Children']:
            child_tr=resolve(tr,child);found=find_animator(resolve(child_tr,source.data(child_tr)['m_GameObject']))
            if found is not None:return found
    animated=find_animator(prefab)
    if animated is None:raise ValueError('Missing Animator in '+recipe['id'])
    walk(animated,-1,'')
    if len(colliders)!=len(collider_ids):raise ValueError('Combat collider is outside the animated hierarchy')
    parts = []
    for renderer, data, static_node in renderers:
        mesh = resolve(renderer, data['m_Mesh']).read()
        handler = MeshHandler(mesh); handler.process()
        weights=indices=bones=bind=None
        if static_node is None:
            weights = np.asarray(handler.m_BoneWeights);indices = np.asarray(handler.m_BoneIndices, dtype=int)
            if weights.shape != (len(handler.m_Vertices), 4) or not np.allclose(weights.sum(axis=1), 1, atol=.01):raise ValueError('Invalid skin weights: '+mesh.m_Name)
            bones = [by_id[resolve(renderer, p).path_id] for p in data['m_Bones']]
            bind = np.asarray([[[getattr(matrix, 'e%d%d' % (row, col)) for col in range(4)] for row in range(4)] for matrix in mesh.m_BindPose])
        materials = []
        for ptr in data['m_Materials']:
            mat = resolve(renderer, ptr)
            props = mat.parse_as_dict()['m_SavedProperties']
            selected = next((v for k,v in props['m_TexEnvs'] if k in ('_MainTex', '_BaseMap', '_AlbedoMap', '_BaseColorMap') and v['m_Texture']['m_PathID']), None)
            texture_name = ''
            if selected:
                texture = resolve(mat, selected['m_Texture'])
                texture_name = str(texture.path_id)+'.png'
                if not (out/texture_name).exists():texture.read().image.save(out/texture_name)
            materials.append({'texture':texture_name,'name':mat.read().m_Name})
        if not materials:raise ValueError('Missing renderer material: '+mesh.m_Name)
        parts.append((np.asarray(handler.m_Vertices)[:,:3], np.asarray(handler.m_Normals)[:,:3], np.asarray(handler.m_UV0)[:,:2], weights, indices, bones, bind, handler.get_triangles(), materials,static_node))
    frames, metadata = [], []
    overall_min, overall_max = np.full(3, math.inf), np.full(3, -math.inf)
    for state,owner,playable in states:
        if playable.get('type',0)!=0 or playable.get('frames',0):raise ValueError('Layered or retimed animation needs an explicit adapter: '+state)
        reader=resolve(owner,playable['clip']);data=source.data(reader);name=data['m_Name'];clip=Clip(data)
        validate_clip_events(data['m_Events'])
        if any(path not in paths for path,_,_,_ in clip.bindings):
            missing = [path for path,_,_,_ in clip.bindings if path not in paths]
            raise ValueError('Animation skeleton mismatch: '+name+' '+str(missing))
        count = max(1, math.ceil(clip.duration*fps-1e-5))
        cues=[sound_recipe(sound,60,source.source_hz) for sound in playable.get('sounds',[])]
        entry = {'state':state,'clip':name,'first':len(frames),'frames':count,'duration':clip.duration,'events':data['m_Events'],'sounds':[c for c in cues if c is not None]}
        root_start = clip.sample(0).get((0,1), nodes[0][2])
        for frame in range(count):
            pose = clip.sample(frame/fps)
            root_delta=pose.get((0,1),root_start)-root_start
            if state.startswith('attack-') and abs(root_delta[1])>.01:raise ValueError('Vertical root motion needs a movement profile: '+name)
            root_motion = float(root_delta[0])*ppu
            worlds = []
            for parent, path, position, rotation, scale in nodes:
                pos = pose.get((path,1), position).copy()
                if path == 0:pos = root_start.copy()
                local = transform(pos, pose.get((path,2), rotation), pose.get((path,3), scale))
                worlds.append(local if parent<0 else worlds[parent]@local)
            frame_parts = []
            for part_id, (vertices,normals,uvs,weights,indices,bones,bind,triangles,materials,static_node) in enumerate(parts):
                hom = np.concatenate([vertices,np.ones((len(vertices),1))],axis=1)
                if static_node is not None:
                    position=(hom@worlds[static_node].T)[:,:3];normal=normals@worlds[static_node][:3,:3].T
                else:
                    skin = np.asarray([worlds[index] for index in bones])@bind
                    position = np.zeros((len(vertices),3)); normal = np.zeros_like(position)
                    for slot in range(4):
                        matrices = skin[indices[:,slot]]
                        position += np.einsum('nij,nj->ni',matrices,hom)[:,:3]*weights[:,slot,None]
                        normal += np.einsum('nij,nj->ni',matrices[:,:3,:3],normals)*weights[:,slot,None]
                normal /= np.maximum(np.linalg.norm(normal,axis=1)[:,None],1e-8)
                if not np.isfinite(position).all():raise ValueError('Invalid skinned pose')
                overall_min = np.minimum(overall_min,position.min(axis=0));overall_max = np.maximum(overall_max,position.max(axis=0))
                filename = '%s-%03d-%d.bin' % (state,frame,part_id)
                with (out/filename).open('wb') as stream:
                    stream.write(struct.pack('<i',len(vertices)))
                    stream.write(np.concatenate([position,normal,uvs],axis=1).astype('<f4').tobytes())
                    stream.write(struct.pack('<i',len(triangles)))
                    for submesh in triangles:
                        values = np.asarray(submesh,dtype='<i4').flatten();stream.write(struct.pack('<i',len(values)));stream.write(values.tobytes())
                frame_parts.append({'mesh':filename,'materials':materials})
            boxes = {'hurt':[],'attack':[]}
            for box_name,index,box in colliders:
                sx,sy = box['m_Size']['x']/2,box['m_Size']['y']/2
                ox,oy = box['m_Offset']['x'],box['m_Offset']['y']
                points = np.asarray([worlds[index]@np.asarray([ox+x,oy+y,0,1]) for x in (-sx,sx) for y in (-sy,sy)])
                lo,hi=points.min(axis=0),points.max(axis=0)
                boxes[box_name].append([math.floor(lo[0]*ppu),math.floor(-hi[1]*ppu),math.ceil(hi[0]*ppu)-math.floor(lo[0]*ppu),math.ceil(-lo[1]*ppu)-math.floor(-hi[1]*ppu)])
            frames.append({'state':state,'frame':frame,'parts':frame_parts,'root':root_motion,'boxes':boxes})
        metadata.append(entry)
        print(state,count,'frames',flush=True)
    hit_sounds=[sound_recipe(sound,60,source.source_hz) for sound in animations.get('hitAnimation',{}).get('sounds',[])]
    action_rules=[]
    for index,(reader,weight) in enumerate(attacks):
        config=source.data(reader);animation=metadata[index+4]
        active=[e['time'] for e in animation['events'] if e['functionName']=='activeStart']
        if len(active)!=1:raise ValueError('Attack needs one activeStart event: '+config['m_Name'])
        hit=resolve(reader,config['hit']);impact_pointers=source.data(hit)['impactConfigs']
        if len(impact_pointers)!=1:raise ValueError('Multiple impact behaviors need an explicit attack adapter')
        impact=source.data(resolve(hit,impact_pointers[0]));damages=impact['damages']
        if not damages:raise ValueError('Missing source attack damage')
        if impact.get('dontConsumeHit',0) or impact.get('selfDamage',0) or any(impact.get('ailments',{}).get(name,{}).get('value',0) for name in ('poison','terror')):raise ValueError('Impact needs a status or multi-hit effect adapter: '+config['m_Name'])
        damage=max(1,round(sum(d['amount'] for d in damages)*recipe['adaptation']['damage_scale']))
        action_rules.append({'id':config['m_Name'],'clip':index+4,'weight':weight,'active_tick':round(active[0]*60),'damage':damage,'sounds':animation['sounds']})
    result={'id':recipe['id'],'fps':fps,'render':render,'adaptation':recipe['adaptation'],'health':health,'behavior':recipe['behavior'],'attacks':action_rules,'hit_sounds':[c for c in hit_sounds if c is not None],'clips':metadata,'frames':frames,'min':overall_min.tolist(),'max':overall_max.tolist()}
    (out/'enemy.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    receipt={'actor':'Niotid','id':recipe['id'],'prefab':recipe['source_prefab'],'brain':recipe['source_brain'],'manifest_sha256':manifest_hash(manifest),'unitypy':UnityPy.__version__,'clips':metadata,'source_hz':source.source_hz,'behavior':recipe['behavior'],'adaptations':recipe['notes'],'source_ai_replaced':True,'inputs':[]}
    for path in source.inputs:
        digest=hashlib.sha256()
        with path.open('rb') as stream:
            for chunk in iter(lambda:stream.read(4*1024*1024),b''):digest.update(chunk)
        receipt['inputs'].append({'name':path.name,'sha256':digest.hexdigest()})
    # the collision table shares the exact atlas order and has no mesh data
    with (out/(recipe['id']+'.bin')).open('wb') as stream:
        stream.write(b'MVE2');stream.write(struct.pack('<iiiii',fps,len(metadata),len(frames),len(source_colliders['hurt']),len(source_colliders['attack'])))
        for entry in metadata:stream.write(struct.pack('<iii',entry['first'],entry['frames'],round(entry['duration']*60)))
        for frame in frames:
            stream.write(struct.pack('<f',frame['root']))
            for kind in ('hurt','attack'):
                for values in frame['boxes'][kind]:
                    if any(abs(v)>256 for v in values):raise ValueError('Collider exceeds the sprite budget')
                    stream.write(struct.pack('<4h',*values))
    (out/'source-receipt.json').write_text(json.dumps(receipt,indent=2),encoding='utf-8')
    print('bounds',overall_min,overall_max,flush=True)
    return result


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--quest-dir',type=Path,required=True);parser.add_argument('--output',type=Path,required=True);parser.add_argument('--manifest',type=Path,default=DEFAULT_MANIFEST)
    args=parser.parse_args();manifest=load_manifest(args.manifest);source=QuestSource(args.quest_dir,manifest)
    result=[import_enemy(source,manifest,recipe,args.output/recipe['id']) for recipe in manifest['enemies']]
    (args.output/'index.json').write_text(json.dumps([{'id':e['id'],'path':e['id']+'/enemy.json'} for e in result],indent=2))


if __name__ == '__main__':main()
