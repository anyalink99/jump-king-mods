"""List source enemy brains for authoring; this does not certify a behavior adapter."""
import argparse
import json
from pathlib import Path
from quest_manifest import DEFAULT_MANIFEST,load_manifest
from quest_source import QuestSource


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--quest-dir',type=Path,required=True);parser.add_argument('--manifest',type=Path,default=DEFAULT_MANIFEST);parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args();source=QuestSource(args.quest_dir,load_manifest(args.manifest));result=[]
    for name,pointer in source.container.items():
        if '/_Networked Entities/' not in name or not name.endswith('.prefab'):continue
        go=pointer.deref()
        for component in source.components(go):
            if component.type.name!='MonoBehaviour' or source.script(component)!='SolventBehaviour':continue
            for value in source.data(component)['brainConfigs']:
                if not value['m_PathID']:continue
                brain=source.resolve(component,value)
                if source.script(brain)!='EnemyBrainConfig':continue
                data=source.data(brain);entry={'prefab':name,'brain':data['m_Name'],'assessment':'metadata only; animation and behavior support still need import validation'}
                try:
                    actions=source.table_actions(brain,data['decisionConfig']['attackTable'])
                    entry['attacks']=[source.data(action)['m_Name'] for action,weight in actions]
                except (ValueError,KeyError) as error:entry['unsupported_decision']=str(error)
                result.append(entry)
    args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(sorted(result,key=lambda x:(x['prefab'],x['brain'])),indent=2),encoding='utf-8')
    print('Source enemy brains:',len(result),'->',args.output)


if __name__=='__main__':main()
