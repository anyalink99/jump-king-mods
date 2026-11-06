"""Read selected prefab graphs; source assemblies are metadata, never executable plugins."""
import struct
import uuid
from pathlib import Path
import UnityPy
from UnityPy.classes.PPtr import PPtr
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator


class QuestSource:
    def __init__(self,quest,manifest):
        self.root=Path(quest)/'JKQ_Data'
        bundles=self.root/'StreamingAssets/aa/StandaloneWindows64'
        self.inputs=[next(bundles.glob(prefix+'*')) for prefix in ('prefabs','materials','items','objects')]
        self.env=UnityPy.load(*(str(p) for p in self.inputs),str(self.root/'globalgamemanagers.assets'),str(self.root/'globalgamemanagers'))
        gen=TypeTreeGenerator(manifest['unity_version']);gen.load_local_dll_folder(str(self.root/'Managed'));self.env.typetree_generator=gen
        while True:
            try:self.container=self.env.container;break
            except RuntimeError as error:
                if 'dictionary changed size' not in str(error):raise
        time=next(o.parse_as_dict() for o in self.env.objects if o.type.name=='TimeManager')['Fixed Timestep']
        self.source_hz=round(1/time)
        if self.source_hz<1 or self.source_hz>240:raise ValueError('Unsupported source timestep')
        self.cache={}

    def resolve(self,obj,pointer):return PPtr(assetsfile=obj.assets_file,**pointer).deref()

    def data(self,obj):
        key=(obj.assets_file.name,obj.path_id)
        if key not in self.cache:self.cache[key]=obj.parse_as_dict()
        return self.cache[key]

    def components(self,go):return [self.resolve(go,p['component']) for p in self.data(go)['m_Component']]

    def script(self,obj):return self.resolve(obj,self.data(obj)['m_Script']).read().m_ClassName

    def prefab(self,suffix):
        found=[p.deref() for name,p in self.container.items() if name==suffix or name.endswith('/'+suffix)]
        if len(found)!=1:raise ValueError('Prefab selection must be unique: '+suffix)
        return found[0]

    def table_actions(self,owner,pointer):
        table=self.resolve(owner,pointer);data=self.data(table);result=[]
        for decision in data['decisions']:
            if decision.get('_event',0) or decision.get('healthPercentage',0) or decision.get('flags',0):raise ValueError('Conditional decision needs another behavior profile')
            if decision['weight']<0:raise ValueError('Unsupported decision weight')
            if decision['weight']==0:continue
            config=self.resolve(table,decision['config'])
            if self.script(config)!='EnemyActionConfig':raise ValueError('Unsupported decision node: '+self.script(config))
            result.append((config,decision['weight']))
        return result

    def enemy(self,recipe):
        prefab=self.prefab(recipe['source_prefab']);components=self.components(prefab)
        behavior=next(c for c in components if c.type.name=='MonoBehaviour' and self.script(c)=='SolventBehaviour')
        choices=[self.resolve(behavior,p) for p in self.data(behavior)['brainConfigs']]
        found=[o for o in choices if self.data(o)['m_Name']==recipe['source_brain']]
        if len(found)!=1:raise ValueError('Missing or ambiguous enemy brain: '+recipe['source_brain'])
        brain=found[0];data=self.data(brain);animations=self.resolve(brain,data['animations']);animation_data=self.data(animations)
        idle=self.table_actions(brain,data['decisionConfig'][recipe['states']['idle']])
        if len(idle)!=1:raise ValueError('Ambiguous idle state; provide a supported state adapter')
        states=[('idle',idle[0][0],self.data(idle[0][0])['animation'])]
        states.extend((name,animations,animation_data[recipe['states'][name]]) for name in ('walk','hurt','death'))
        attacks=self.table_actions(brain,data['decisionConfig']['attackTable'])
        if not 1<=len(attacks)<=8:raise ValueError('Attack count exceeds profile budget')
        for action,weight in attacks:
            action_data=self.data(action)
            if any(action_data.get(k,0) for k in ('flipOnCompletion','dieOnCompletion','allowJumpBetweenTarget','isRigid')) or action_data.get('hyperArmor',{}).get('resistance',0):
                raise ValueError('Unsupported attack mechanic: '+action_data['m_Name'])
            states.append(('attack-'+str(len(states)-4),action,action_data['animation']))
        combatant=next(c for c in components if c.type.name=='MonoBehaviour' and self.script(c)=='SolventCombatantBehavior')
        combat=self.data(combatant)
        colliders={name:[self.resolve(combatant,p) for p in combat[field]['collider']['colliders'] if p['m_PathID']] for name,field in [('hurt','hurtBox'),('attack','attackBox')]}
        if any(not 1<=len(values)<=8 for values in colliders.values()):raise ValueError('Collider count exceeds profile budget')
        status=self.resolve(brain,data['status'])
        return prefab,states,attacks,colliders,self.data(status)['health']['maxValue'],animation_data


def sound_guid(sound):
    value=sound['eventRef']['Guid']
    return str(uuid.UUID(bytes_le=struct.pack('<4I',*(value['Data'+str(i)]&0xffffffff for i in range(1,5)))))


def sound_recipe(sound,hz,source_hz):
    guid=sound_guid(sound)
    if guid=='00000000-0000-0000-0000-000000000000':return None
    if sound['type'] not in (0,5):raise ValueError('Unsupported sound trigger type: '+str(sound['type']))
    return {'id':'jkq.'+guid,'event_guid':guid,'tick':round(sound['startFrame']*hz/source_hz),'stoppable':bool(sound['isStoppable'])}


def validate_clip_events(events):
    for event in events:
        if event['functionName']!='activeStart' or event.get('data') or event.get('intParameter',0) or event.get('floatParameter',0) or event.get('objectReferenceParameter',{}).get('m_PathID',0):
            raise ValueError('Animation event needs an explicit adapter: '+event['functionName'])
