# Blender script: reduce Tripo towers (~1.9M faces) to game-ready LODs.
# Usage: blender -b -P decimate_pc_towers.py -- <folder_with_PC_glb>
import bpy, sys, os
folder = sys.argv[sys.argv.index("--") + 1]
TARGETS = {"PC": 60000, "Mobile": 8000}   # faces per tower
for f in sorted(os.listdir(folder)):
    if not f.endswith("_PC.glb"): continue
    for tag, target in TARGETS.items():
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=os.path.join(folder, f))
        for ob in [o for o in bpy.context.scene.objects if o.type == "MESH"]:
            faces = len(ob.data.polygons)
            if faces > target:
                mod = ob.modifiers.new("dec", "DECIMATE"); mod.ratio = target / faces
                bpy.context.view_layer.objects.active = ob
                bpy.ops.object.modifier_apply(modifier="dec")
        out = os.path.join(folder, f.replace("_PC.glb", f"_{tag}_LOD.glb"))
        bpy.ops.export_scene.gltf(filepath=out, export_format="GLB")
        print("wrote", out)
