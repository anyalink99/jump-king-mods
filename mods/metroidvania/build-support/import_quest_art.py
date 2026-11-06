"""Decode selected Quest models and icons for the offline sprite baker."""
import argparse
import hashlib
import json
import struct
from pathlib import Path
import UnityPy
from UnityPy.classes.PPtr import PPtr
from UnityPy.helpers.MeshHelper import MeshHandler
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
from quest_manifest import load_manifest,DEFAULT_MANIFEST

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--quest-dir", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True)
parser.add_argument("--manifest",type=Path,default=DEFAULT_MANIFEST)
args = parser.parse_args()
manifest=load_manifest(args.manifest)
root = args.quest_dir / "JKQ_Data/StreamingAssets/aa/StandaloneWindows64"
out = args.output / "models"
out.mkdir(parents=True, exist_ok=True)
inputs = [next(root.glob(prefix + "*")) for prefix in ("items", "prefabs", "materials")]
env = UnityPy.load(*(str(path) for path in inputs))
generator = TypeTreeGenerator(manifest['unity_version'])
generator.load_local_dll_folder(str(args.quest_dir / "JKQ_Data/Managed"))
env.typetree_generator = generator
try:env.container
except RuntimeError:pass
first=next(item['source'] for item in manifest['items'] if 'source' in item)
items_asset = next(ptr.deref().assets_file for key, ptr in env.container.items() if key.endswith('/'+first+'.prefab'))
selected = {item['source'] for item in manifest['items'] if 'source' in item}
items = {}
for obj in items_asset.objects.values():
    if obj.type.name != "MonoBehaviour":
        continue
    head = obj.parse_monobehaviour_head()
    if head.m_Script.read().m_ClassName != "ItemAsset":
        continue
    name = head.m_GameObject.read().m_Name
    if name not in selected:
        continue
    tree = obj.parse_as_dict()
    items[name] = (obj, tree)
    icon = PPtr(assetsfile=obj.assets_file, **tree["icon"]).read()
    icon.image.save(args.output / (name + "-icon.png"))
    (args.output / (name + ".json")).write_text(json.dumps(tree, indent=2), encoding="utf-8")
if set(items) != selected:
    raise ValueError("Quest item catalogue changed: " + repr(selected - set(items)))
for item in manifest['items']:
    if 'source_sprite' in item:
        sprite=next(obj for obj in items_asset.objects.values() if obj.type.name=='Sprite' and obj.read().m_Name==item['source_sprite'])
        sprite.read().image.save(args.output/(item['id']+'-icon.png'))
(args.output/'items.json').write_text(json.dumps(manifest['items'],indent=2),encoding='utf-8')

def resolve(parent, pointer):
    return PPtr(assetsfile=parent.assets_file, **pointer).deref()

def export_part(reader, data, transforms, parts):
    mesh_pointer = data.get("m_Mesh")
    if mesh_pointer is None or not mesh_pointer["m_PathID"]:
        return
    try:
        mesh = resolve(reader, mesh_pointer).read()
    except FileNotFoundError as error:
        if "unity default resources" in str(error):
            return
        raise
    handler = MeshHandler(mesh)
    handler.process()
    name = str(reader.path_id)
    file = out / (name + ".bin")
    triangles = handler.get_triangles()
    with file.open("wb") as stream:
        stream.write(struct.pack("<i", len(handler.m_Vertices)))
        for i, vertex in enumerate(handler.m_Vertices):
            normal = handler.m_Normals[i] if handler.m_Normals else (0,0,1)
            uv = handler.m_UV0[i] if handler.m_UV0 else (0,0)
            stream.write(struct.pack("<8f", *vertex[:3], *normal[:3], *uv[:2]))
        stream.write(struct.pack("<i", len(triangles)))
        for submesh in triangles:
            stream.write(struct.pack("<i", len(submesh)*3))
            for tri in submesh:
                stream.write(struct.pack("<3i", *tri))
    materials = []
    for ptr in data["m_Materials"]:
        mat = resolve(reader, ptr)
        props = mat.read_typetree()["m_SavedProperties"]
        texture = None
        selected = ""
        for prop, value in props["m_TexEnvs"]:
            if prop in ("_MainTex", "_BaseMap", "_AlbedoMap", "_BaseColorMap") and value["m_Texture"]["m_PathID"]:
                texture = resolve(mat, value["m_Texture"])
                selected = prop
                break
        texture_name = ""
        if texture:
            texture_name = str(texture.path_id) + ".png"
            if not (out / texture_name).exists():
                texture.read().image.save(out / texture_name)
        print("material", mat.read().m_Name, selected, texture_name, flush=True)
        materials.append({"texture": texture_name, "name": mat.read().m_Name})
    parts.append({"mesh": file.name, "transforms": transforms, "materials": materials, "name": mesh.m_Name})

def walk(go, ancestors, parts):
    tree = go.read_typetree()
    components = [resolve(go, pair["component"]) for pair in tree["m_Component"]]
    transform = next(c for c in components if c.type.name == "Transform")
    tr = transform.read_typetree()
    transforms = ancestors + [{"position": tr["m_LocalPosition"], "rotation": tr["m_LocalRotation"], "scale": tr["m_LocalScale"]}]
    for component in components:
        data = component.read_typetree() if component.type.name in ("SkinnedMeshRenderer", "MeshRenderer", "MeshFilter") else None
        if data is not None and component.type.name == "SkinnedMeshRenderer":
            export_part(component, data, transforms, parts)
        if data is not None and component.type.name == "MeshFilter":
            renderer = next(c for c in components if c.type.name == "MeshRenderer")
            render_data = renderer.read_typetree()
            render_data["m_Mesh"] = data["m_Mesh"]
            export_part(component, render_data, transforms, parts)
    for child in tr["m_Children"]:
        child_tr = resolve(transform, child)
        walk(resolve(child_tr, child_tr.read_typetree()["m_GameObject"]), transforms, parts)

for name, (reader, item) in sorted(items.items()):
    pointer = item["prefab"]
    parts = []
    walk(resolve(reader, pointer), [], parts)
    if not parts:
        raise ValueError("No renderable mesh for " + name)
    (out / (name + ".json")).write_text(json.dumps(parts, indent=2), encoding="utf-8")
    print(name, "parts", len(parts), flush=True)
(out/'index.json').write_text(json.dumps([name+'.json' for name in sorted(items)]),encoding='utf-8')
receipt = {"unitypy": UnityPy.__version__, "typetree_unity_version": "2021.3.33f1", "items": sorted(items), "inputs": []}
for path in inputs:
    with path.open("rb") as stream:
        hasher = hashlib.sha256()
        for chunk in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            hasher.update(chunk)
        digest = hasher.hexdigest()
    receipt["inputs"].append({"name": path.name, "sha256": digest, "bytes": path.stat().st_size})
(args.output / "source-receipt.json").write_text(json.dumps(receipt, indent=2), encoding="utf-8")
