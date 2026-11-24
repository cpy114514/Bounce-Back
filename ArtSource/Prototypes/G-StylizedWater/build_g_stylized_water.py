"""Rebuild G-StylizedWater meshes and editable Blender source.

Blender 5.3 (also compatible with 4.5):
  blender --background --factory-startup --python-exit-code 1 --python "ArtSource/Prototypes/G-StylizedWater/build_g_stylized_water.py"

Coordinates in the authored geometry are Unity metres. The FBX conversion retains
an internal axis node; use the supplied Unity prefabs, not a hand-normalized FBX.
No colliders, gameplay scripts, external textures, or cameras are exported.
"""
from pathlib import Path
import json
import math
import bpy
from mathutils import Matrix, Vector

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[2]
DEST = PROJECT / "Assets" / "Scenes" / "Archive" / "BounceBack" / "Art" / "G-StylizedWater"
for folder in (DEST / "FBX", DEST / "Documentation"):
    folder.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.name = "G-StylizedWater"
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.unit_settings.length_unit = "METERS"
collection = bpy.data.collections.new("01_STATIC_WATER_MESHES")
scene.collection.children.link(collection)
# Blender is right-handed and Unity is left-handed. The FBX exporter reflects
# Blender X back to Unity X. Author with (-x,-z,y) and reverse polygon winding.
BASIS = Matrix(((-1, 0, 0), (0, 0, -1), (0, 1, 0)))

PALETTE = {
    "MV_Water": (0.45, 0.80, 0.84),
    "MV_Ivory": (0.95, 0.88, 0.77),
    "MV_Lilac": (0.50, 0.45, 0.66),
}


def linear(x):
    return x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4


materials = []
for name, color in PALETTE.items():
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = (*color, 1.0)
    material["sRGB"] = list(color)
    material["shader_color_linear"] = [linear(c) for c in color]
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*[linear(c) for c in color], 1.0)
    shader.inputs["Roughness"].default_value = 0.5
    materials.append(material)


def make_mesh(name, vertices, faces, uv_by_vertex, anchor, note):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    # Convert to anchor-local Blender space. Blender Z is Unity Y.
    mesh.from_pydata([BASIS @ (Vector(v) - Vector(anchor)) for v in vertices], [],
                     [tuple(reversed(face)) for face in faces])
    mesh.update()
    mesh.materials.append(materials[0])
    uv = mesh.uv_layers.new(name="UV0_Flow")
    for polygon in mesh.polygons:
        polygon.material_index = 0
        polygon.use_smooth = False
        for loop_index in polygon.loop_indices:
            index = mesh.loops[loop_index].vertex_index
            uv.data[loop_index].uv = uv_by_vertex[index]
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj["unity_anchor"] = list(anchor)
    obj["pivot"] = note
    obj["visual_only"] = True
    obj["contains_colliders"] = False
    # Recalculate winding for open water cards without closing the surface.
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    return obj


POOL_CENTER = (-1.4, 0.45, 2.05)
POOL_SIZE = (3.25, 3.65)
NX, NZ = 32, 36
vertices, faces, uvs = [], [], []
for z in range(NZ + 1):
    v = z / NZ
    for x in range(NX + 1):
        u = x / NX
        vertices.append(((u - 0.5) * POOL_SIZE[0], 0.0, (v - 0.5) * POOL_SIZE[1]))
        uvs.append((u, v))
for z in range(NZ):
    for x in range(NX):
        a = z * (NX + 1) + x
        faces.append((a, a + NX + 1, a + NX + 2, a + 1))
pool = make_mesh("G_PoolSurface", vertices, faces, uvs, (0, 0, 0),
                 "Courtyard and Water Art at (0,0,0); mesh top center (-1.4,0.45,2.05)")
pool.location = BASIS @ Vector(POOL_CENTER)
pool["water_level_unity_y"] = 0.45
pool["rest_plane_error_m"] = 0.0

# The outlet slot has identity rotation. Local +X is outward, +Y up, Z across.
# The water stays at or below Y=0.43 and does not obstruct the pressure plate.
COLS, ROWS = 8, 40
WIDTH, DROP = 1.60, 10.0
vertices, faces, uvs = [], [], []
for row in range(ROWS + 1):
    v = row / ROWS
    # A short lip roll, then a nearly vertical fall with a subtle outward bow.
    x = 0.18 * (1.0 - math.exp(-v * 30.0)) + 0.09 * math.sin(v * math.pi)
    for col in range(COLS + 1):
        u = col / COLS
        across = (u - 0.5) * WIDTH * (1.0 + 0.06 * v)
        fold = 0.014 * math.cos(u * math.pi * 6.0) * math.sin(v * math.pi)
        vertices.append((x + fold, -v * DROP, across))
        uvs.append((u, v))
for row in range(ROWS):
    for col in range(COLS):
        a = row * (COLS + 1) + col
        faces.append((a, a + 1, a + COLS + 2, a + COLS + 1))
falls = make_mesh("G_WaterfallRibbon", vertices, faces, uvs, (0, 0, 0),
                  "Waterfall Slot (5.72,0.43,5.24), identity rotation, local +X outward")
falls["outlet_unity"] = [5.72, 0.43, 5.24]
falls["drop_m"] = DROP

# Three very cheap soft mist cards, all sharing a single procedural material.
# They are vertical/crossed only in the static mesh; no particle system is needed.
vertices, faces, uvs = [], [], []
for card, (center, axis_u, axis_v, size_u, size_v) in enumerate([
    ((0.34, -9.72, 0.0), (0, 0, 1), (0, 1, 0), 1.70, 0.65),
    ((0.38, -9.68, 0.0), (0.80, 0, 0.60), (0, 1, 0), 1.50, 0.70),
    ((0.35, -9.78, 0.0), (1, 0, 0), (0, 0, 1), 0.95, 1.30),
]):
    start = len(vertices)
    for u, v in ((0, 0), (1, 0), (1, 1), (0, 1)):
        point = Vector(center) + Vector(axis_u) * ((u - 0.5) * 2 * size_u) + Vector(axis_v) * ((v - 0.5) * 2 * size_v)
        vertices.append(tuple(point))
        uvs.append((u, v))
    faces.append(tuple(start + i for i in (0, 1, 2, 3)))
mist = make_mesh("G_WaterfallMist", vertices, faces, uvs, (0, 0, 0),
                 "Same Waterfall Slot; three 2-triangle soft cards, no ParticleSystem")
mist["contains_particle_system"] = False

# Set a useful source-file inspection camera; never exported.
preview = bpy.data.collections.new("90_SOURCE_INSPECTION_ONLY")
scene.collection.children.link(preview)
cam_data = bpy.data.cameras.new("G_Source_Inspection")
cam = bpy.data.objects.new("G_Source_Inspection", cam_data)
preview.objects.link(cam)
cam.location = (13, -19, 8)
cam.rotation_euler = (Vector((0, 0, -3.2)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam_data.type = 'ORTHO'
cam_data.ortho_scale = 22
scene.camera = cam
world = bpy.data.worlds.new("G_Source_Sky")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.65, 0.78, 0.86, 1)
scene.world = world

for obj in (pool, falls, mist):
    obj.select_set(True)
bpy.context.view_layer.objects.active = pool
kwargs = dict(filepath=str(DEST / "FBX" / "G-StylizedWater.fbx"), use_selection=True,
              object_types={"MESH"}, global_scale=1.0, apply_unit_scale=True,
              axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
              add_leaf_bones=False, path_mode="AUTO", bake_anim=False)
supported = bpy.ops.export_scene.fbx.get_rna_type().properties.keys()
bpy.ops.export_scene.fbx(**{key: value for key, value in kwargs.items() if key in supported})
bpy.ops.object.select_all(action="DESELECT")
pool.select_set(True)
bpy.context.view_layer.objects.active = pool
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "G-StylizedWater.blend"), check_existing=False)


def report(obj, local_anchor=(0, 0, 0)):
    coords = [BASIS.transposed() @ (obj.location + v.co) + Vector(local_anchor) for v in obj.data.vertices]
    return {
        "vertices": len(obj.data.vertices),
        "triangles": sum(len(p.vertices) - 2 for p in obj.data.polygons),
        "material_slots": 1,
        "bounds_local_unity": {
            "min": [min(v[i] for v in coords) for i in range(3)],
            "max": [max(v[i] for v in coords) for i in range(3)],
        },
        "external_textures": [],
        "contains_colliders": False,
    }

manifest = {
    "task": "G-StylizedWater",
    "source_blend": "ArtSource/Prototypes/G-StylizedWater/G-StylizedWater.blend",
    "rebuild_script": "ArtSource/Prototypes/G-StylizedWater/build_g_stylized_water.py",
    "fbx": "Assets/Scenes/Archive/BounceBack/Art/G-StylizedWater/FBX/G-StylizedWater.fbx",
    "units": "metres; Unity +Y up, +Z forward",
    "pool": {
        "prefab_anchor_unity": [0, 0, 0], "anchor_name": "Courtyard and Water Art",
        "center_unity": list(POOL_CENTER), "size_xz_m": list(POOL_SIZE),
        "flat_surface_y": 0.45, "maximum_wave_downward_m": 0.018,
        "rest_plane_error_m": 0.0, "collision_item": "Elastic Pool Surface",
    },
    "waterfall": {
        "prefab_anchor_unity": [5.72, 0.43, 5.24], "anchor_name": "Waterfall Slot",
        "root_rotation_euler": [0, 0, 0], "outward_local_axis": "+X",
        "width_m": WIDTH, "drop_m": DROP, "mist_card_count": 3,
        "slot_origin_error_m": 0.0, "contains_particle_system": False,
    },
    "mesh_parts": {obj.name: report(obj) for obj in (pool, falls, mist)},
    "total_triangles": sum(len(p.vertices) - 2 for obj in (pool, falls, mist) for p in obj.data.polygons),
    "animation": "Shader time only. No baked animation, Compute, screen texture or depth texture.",
}
(DEST / "Documentation" / "G-StylizedWater_Geometry.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
print("G_STYLIZED_WATER_BUILD_COMPLETE", json.dumps(manifest["mesh_parts"]), "TOTAL", manifest["total_triangles"])
