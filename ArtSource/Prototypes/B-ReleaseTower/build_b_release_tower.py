"""Build B-ReleaseTower: a slender three-stage release tower and bell pavilion.

Run with Blender 5.x:
  blender.exe --background --factory-startup --python-exit-code 1 --python build_b_release_tower.py

All delivery geometry is authored in metres relative to the Unity anchor
(-5.64, -0.22, -3.65). Unity axes: X right, +Y up, +Z forward.
The FBX axis conversion stays inside the imported child, never on the outer prefab.
"""
from pathlib import Path
import json
import math
import bpy
import bmesh
from mathutils import Matrix, Vector

SOURCE_DIR = Path(__file__).resolve().parent
PROJECT_DIR = SOURCE_DIR.parents[2]
DELIVERY_DIR = PROJECT_DIR / "Assets" / "Scenes" / "Archive" / "BounceBack" / "Art" / "B-ReleaseTower"
FBX_DIR = DELIVERY_DIR / "FBX"
PREVIEW_DIR = DELIVERY_DIR / "Preview"
DOC_DIR = DELIVERY_DIR / "Documentation"
for folder in (FBX_DIR, PREVIEW_DIR, DOC_DIR):
    folder.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.name = "B_Release_Tower_Source"
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.unit_settings.length_unit = "METERS"

COLLECTIONS = {}
for name in ("01_BODY", "02_LAUNCH_PLATFORM", "03_BELL_TOWER", "90_PREVIEW_ONLY"):
    collection = bpy.data.collections.new(name)
    scene.collection.children.link(collection)
    COLLECTIONS[name] = collection

# Left-handed Unity to right-handed Blender: reflect X, then map Y-up to Z-up.
# Blender's -Z-forward/Y-up FBX export and Unity import round-trip to the original X.
C = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
CI = C.inverted()
ANCHOR = Vector((-5.64, -0.22, -3.65))


def bp(point):
    return (C @ Vector((*point, 1.0))).to_3d()


def up(point):
    return (CI @ Vector((*point, 1.0))).to_3d()


def local_y(world_y):
    return world_y - ANCHOR.y


def srgb_linear(value):
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


def material(name, srgb, roughness=0.76):
    linear = tuple(srgb_linear(value) for value in srgb)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = (*linear, 1.0)
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*linear, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = 0.0
    shader.inputs["Specular IOR Level"].default_value = 0.25
    return mat


PALETTE = {
    "MV_Ivory": (0.95, 0.88, 0.77),
    "MV_IvoryShade": (0.86, 0.76, 0.70),
    "MV_Rose": (0.74, 0.45, 0.47),
    "MV_RoseDeep": (0.55, 0.30, 0.38),
    "MV_Lilac": (0.50, 0.45, 0.66),
    "MV_Apricot": (0.90, 0.58, 0.37),
    "MV_DomeBlue": (0.30, 0.47, 0.80),
    "MV_Mint": (0.36, 0.70, 0.55),
    "MV_Water": (0.45, 0.80, 0.84),
    "MV_Gold": (0.96, 0.70, 0.32),
}
MATS = {name: material(name, color) for name, color in PALETTE.items()}


def yaw_point(point, degrees):
    angle = math.radians(degrees)
    cosine, sine = math.cos(angle), math.sin(angle)
    x, y, z = point
    return (cosine * x + sine * z, y, -sine * x + cosine * z)


class MeshBuilder:
    def __init__(self):
        self.vertices = []
        self.faces = []
        self.material_indices = []
        self.smooth = []

    def add(self, vertices, faces, mat=0, smooth=False):
        offset = len(self.vertices)
        self.vertices.extend(tuple(v) for v in vertices)
        self.faces.extend(tuple(index + offset for index in face) for face in faces)
        self.material_indices.extend([mat] * len(faces))
        self.smooth.extend([smooth] * len(faces))

    def box(self, center, size, mat=0, yaw=0.0):
        cx, cy, cz = center
        sx, sy, sz = (value * 0.5 for value in size)
        vertices = [(cx + x * sx, cy + y * sy, cz + z * sz)
                    for x, y, z in ((-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),
                                    (-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1))]
        if yaw:
            vertices = [yaw_point(v, yaw) for v in vertices]
        faces = ((0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7))
        self.add(vertices, faces, mat)

    def prism_xy(self, polygon, z_min, z_max, mat=0):
        count = len(polygon)
        vertices = [(x,y,z_min) for x,y in polygon] + [(x,y,z_max) for x,y in polygon]
        faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
        for index in range(count):
            nxt = (index + 1) % count
            faces.append((index, nxt, nxt + count, index + count))
        self.add(vertices, faces, mat)

    def lathe_ellipse(self, profile, center=(0,0,0), segments=16, mat=0, phase=0.0, smooth=True):
        cx, cy, cz = center
        vertices, rings = [], []
        for radius_x, radius_z, height in profile:
            if max(radius_x, radius_z) <= 0.000001:
                rings.append([len(vertices)])
                vertices.append((cx, cy + height, cz))
                continue
            ring = []
            for segment in range(segments):
                angle = phase + 2.0 * math.pi * segment / segments
                ring.append(len(vertices))
                vertices.append((cx + radius_x * math.cos(angle), cy + height,
                                 cz + radius_z * math.sin(angle)))
            rings.append(ring)
        faces = []
        for first, second in zip(rings, rings[1:]):
            for segment in range(segments):
                nxt = (segment + 1) % segments
                if len(first) == 1:
                    faces.append((first[0], second[nxt], second[segment]))
                elif len(second) == 1:
                    faces.append((first[segment], first[nxt], second[0]))
                else:
                    faces.append((first[segment], first[nxt], second[nxt], second[segment]))
        if len(rings[0]) > 1:
            faces.append(tuple(reversed(rings[0])))
        if len(rings[-1]) > 1:
            faces.append(tuple(rings[-1]))
        self.add(vertices, faces, mat, smooth)

    def append_transformed(self, other, origin=(0,0,0), yaw=0.0):
        vertices = []
        for vertex in other.vertices:
            rotated = yaw_point(vertex, yaw) if yaw else vertex
            vertices.append(tuple(rotated[i] + origin[i] for i in range(3)))
        offset = len(self.vertices)
        self.vertices.extend(vertices)
        self.faces.extend(tuple(index + offset for index in face) for face in other.faces)
        self.material_indices.extend(other.material_indices)
        self.smooth.extend(other.smooth)


def arch_wall(builder, origin, width, height, depth, opening_width, opening_height,
              bottom=0.0, yaw=0.0, mat=0, segments=8):
    part = MeshBuilder()
    radius = opening_width * 0.5
    spring = bottom + opening_height - radius
    pier_width = (width - opening_width) * 0.5
    part.box((-(radius + pier_width * 0.5), height * 0.5, 0), (pier_width,height,depth), mat)
    part.box(((radius + pier_width * 0.5), height * 0.5, 0), (pier_width,height,depth), mat)
    if bottom > 0.0001:
        part.box((0,bottom * 0.5,0), (opening_width,bottom,depth), mat)
    for index in range(segments):
        x0 = -radius + opening_width * index / segments
        x1 = -radius + opening_width * (index + 1) / segments
        y0 = spring + math.sqrt(max(0.0, radius * radius - x0 * x0))
        y1 = spring + math.sqrt(max(0.0, radius * radius - x1 * x1))
        part.prism_xy(((x0,y0),(x1,y1),(x1,height),(x0,height)), -depth * 0.5, depth * 0.5, mat)
    builder.append_transformed(part, origin, yaw)


def arch_panel(builder, origin, width, height, bottom=0.0, depth=0.012, yaw=0.0, mat=0, segments=8):
    radius = width * 0.5
    spring = bottom + height - radius
    polygon = [(-radius,bottom),(radius,bottom)]
    for index in range(segments + 1):
        angle = math.pi * index / segments
        polygon.append((radius * math.cos(angle), spring + radius * math.sin(angle)))
    part = MeshBuilder()
    part.prism_xy(polygon, -depth * 0.5, depth * 0.5, mat)
    builder.append_transformed(part, origin, yaw)


def triangular_prism(builder, points, thickness, mat=0):
    # Triangle lies in XY; thickness is along Z.
    vertices = [(x,y,z - thickness * 0.5) for x,y,z in points] + [(x,y,z + thickness * 0.5) for x,y,z in points]
    faces = [(2,1,0),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)]
    builder.add(vertices, faces, mat)


def create_root(name, collection, notes):
    root = bpy.data.objects.new(name, None)
    COLLECTIONS[collection].objects.link(root)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.22
    root["task"] = "B-ReleaseTower"
    root["anchor_world"] = list(ANCHOR)
    root["pivot"] = "Tower bottom centre, local (0,0,0)"
    root["visual_only"] = True
    root["contains_colliders"] = False
    root["notes"] = notes
    return root


def create_mesh(name, builder, materials, root, collection):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata([bp(v) for v in builder.vertices], [], builder.faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    for mat in materials:
        mesh.materials.append(mat)
    for polygon, index, smooth in zip(mesh.polygons, builder.material_indices, builder.smooth):
        polygon.material_index = index
        polygon.use_smooth = smooth
    uv = mesh.uv_layers.new(name="UV0_Metric")
    for polygon in mesh.polygons:
        dominant = max(range(3), key=lambda axis: abs(polygon.normal[axis]))
        axes = ((1,2),(0,2),(0,1))[dominant]
        for loop_index in polygon.loop_indices:
            vertex = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            uv.data[loop_index].uv = (vertex[axes[0]], vertex[axes[1]])
    obj = bpy.data.objects.new(name, mesh)
    COLLECTIONS[collection].objects.link(obj)
    obj.parent = root
    obj["visual_only"] = True
    obj["contains_colliders"] = False
    return obj


# --- 01. Exact tower core and a three-stage tapered facade. ---
body_root = create_root("BReleaseTower_Body", "01_BODY",
    "Exact 1.77 x 4.02 x 1.79 m reference core plus three tapered stages, two apricot intermediate cornices and six recessed arched windows.")
body_materials = [MATS["MV_Ivory"], MATS["MV_Rose"], MATS["MV_Apricot"], MATS["MV_DomeBlue"]]
core = MeshBuilder()
core.box((0,2.49,0), (1.77,4.02,1.79), 0)
core_obj = create_mesh("Body_Collision_Aligned_Core", core, [MATS["MV_Ivory"]], body_root, "01_BODY")
core_obj["alignment_size_m"] = [1.77,4.02,1.79]
core_obj["alignment_center_local"] = [0,2.49,0]

facade = MeshBuilder()
# 0.50 m plinth: local Y 0..0.50 == world Y -0.22..0.28.
facade.box((0,0.06,0), (2.34,0.12,2.36), 1)
facade.box((0,0.26,0), (2.20,0.28,2.22), 1)
facade.box((0,0.45,0), (2.26,0.10,2.28), 2)

segments = [
    # bottom world, top world, outer X, outer Z, window bottom world, window width, window height
    (0.28,1.46,2.13,2.15,0.52,0.36,0.78),
    (1.58,2.72,1.97,1.99,1.82,0.34,0.76),
    (2.84,4.09,1.85,1.87,3.04,0.32,0.86),
]
for bottom, top, outer_x, outer_z, window_bottom, window_width, window_height in segments:
    height = top - bottom
    for face in ("-Z","+X","+Z","-X"):
        is_x = face in ("+X","-X")
        sign = 1.0 if face in ("+X","+Z") else -1.0
        outer_half = (outer_x if is_x else outer_z) * 0.5
        core_half = (1.77 if is_x else 1.79) * 0.5
        depth = outer_half - core_half + 0.02
        centre_half = (outer_half + core_half) * 0.5
        yaw = {"+Z":0,"+X":90,"-Z":180,"-X":-90}[face]
        origin = (sign * centre_half, local_y(bottom), 0) if is_x else (0,local_y(bottom),sign * centre_half)
        width = outer_z if is_x else outer_x
        if face in ("-Z","+X"):
            arch_wall(facade, origin, width, height, depth, window_width, window_height,
                      bottom=window_bottom-bottom, yaw=yaw, mat=0, segments=8)
            panel_origin = (sign * (core_half + 0.008), local_y(bottom), 0) if is_x else (0,local_y(bottom),sign * (core_half + 0.008))
            arch_panel(facade, panel_origin, window_width + 0.018, window_height + 0.012,
                       bottom=window_bottom-bottom-0.004, yaw=yaw, mat=3, segments=8)
            # A shallow apricot sill remains below the arch and outside the exact core.
            sill_origin = (sign * (outer_half + 0.012), local_y(window_bottom)-0.035, 0) if is_x else (0,local_y(window_bottom)-0.035,sign * (outer_half + 0.012))
            sill_size = (0.05,0.07,window_width+0.16) if is_x else (window_width+0.16,0.07,0.05)
            facade.box(sill_origin, sill_size, 2)
        else:
            part = MeshBuilder()
            part.box((0,height*0.5,0), (width,height,depth), 0)
            facade.append_transformed(part, origin, yaw)

for y, width, depth in ((1.46,2.18,2.20),(2.72,2.02,2.04)):
    facade.box((0,local_y(y+0.06),0), (width,0.12,depth), 2)
facade.box((0,local_y(4.05),0), (1.92,0.10,1.94), 2)
facade_obj = create_mesh("Body_Three_Stage_Facade", facade, body_materials, body_root, "01_BODY")

# --- 02. Exact Launch Coping and low, broken perimeter railings. ---
platform_root = create_root("BReleaseTower_Platform", "02_LAUNCH_PLATFORM",
    "Launch Coping exact top Y=4.45 and bottom Y=4.09. Railings are 0.30 m high and open along the launch lane and east exit.")
slab = MeshBuilder()
# Exact footprint; three material courses do not alter any reference surface.
slab.box((-0.27,local_y(4.13),0), (2.83,0.08,1.92), 0)  # Rose
slab.box((-0.27,local_y(4.20),0), (2.83,0.06,1.92), 1)  # Apricot
slab.box((-0.27,local_y(4.34),0), (2.83,0.22,1.92), 2)  # Ivory top
slab_obj = create_mesh("Launch_Coping_Aligned", slab,
    [MATS["MV_Rose"],MATS["MV_Apricot"],MATS["MV_Ivory"]], platform_root, "02_LAUNCH_PLATFORM")
slab_obj["alignment_center_local"] = [-0.27,4.49,0]
slab_obj["alignment_size_m"] = [2.83,0.36,1.92]
slab_obj["top_world_y"] = 4.45

rails = MeshBuilder()
rail_bottom = local_y(4.45)
# Continuous low masonry rail with a thin cap; overall height 0.30 m.
def rail_box(center, size, cap=True):
    x,y,z = center
    sx,sy,sz = size
    rails.box((x,rail_bottom+sy*0.5,z), (sx,sy,sz), 0)
    if cap:
        rails.box((x,rail_bottom+sy+0.035,z), (sx+0.02,0.07,sz+0.02), 1)

# North/south edges are outside the 0.60 m lane envelope.
rail_box((-0.27,0,-0.91), (2.63,0.23,0.10))
rail_box((-0.27,0,0.91), (2.63,0.23,0.10))
# West edge stays behind the piston's X=-7.20 exclusion plane.
rail_box((-1.635,0,0), (0.10,0.23,1.72))
# East edge exists only at the two corners; the launch exit is completely open.
rail_box((1.095,0,-0.84), (0.10,0.23,0.24))
rail_box((1.095,0,0.84), (0.10,0.23,0.24))
# Four restrained corner piers, capped at Y=4.75.
for x in (-1.635,1.095):
    for z in (-0.91,0.91):
        rails.box((x,rail_bottom+0.15,z), (0.14,0.30,0.14), 0)
        rails.box((x,rail_bottom+0.315,z), (0.16,0.03,0.16), 1)
rail_obj = create_mesh("Platform_Broken_Railings", rails,
    [MATS["MV_Ivory"],MATS["MV_Apricot"]], platform_root, "02_LAUNCH_PLATFORM")
rail_obj["maximum_height_above_coping_m"] = 0.33

# --- 03. Slim bell pavilion, blue dome, finial and folded apricot flag. ---
bell_root = create_root("BReleaseTower_BellTower", "03_BELL_TOWER",
    "North-west pavilion, fully outside the piston footprint: X[-7.28,-6.54], Z[-4.58,-4.08], top Y=8.14.")
bell = MeshBuilder()
BX, BZ = -1.27, -0.68  # world (-6.91,-4.33)
bell.box((BX,local_y(4.515),BZ), (0.72,0.13,0.50), 0)
bell.box((BX,local_y(4.61),BZ), (0.64,0.06,0.46), 1)
bell.box((BX,local_y(5.19),BZ), (0.56,1.22,0.40), 0)
# Narrow blue shaft windows on the two formal-camera faces.
arch_panel(bell, (BX,local_y(4.58),BZ-0.206), 0.17,0.70,bottom=0.22,yaw=180,mat=2,segments=8)
arch_panel(bell, (BX+0.286,local_y(4.58),BZ), 0.15,0.70,bottom=0.22,yaw=90,mat=2,segments=8)
bell.box((BX,local_y(5.86),BZ), (0.64,0.12,0.46), 1)
# Open belfry, four arched walls, no hidden solid core.
for face in ("-Z","+X","+Z","-X"):
    is_x = face in ("+X","-X")
    sign = 1.0 if face in ("+X","+Z") else -1.0
    yaw = {"+Z":0,"+X":90,"-Z":180,"-X":-90}[face]
    origin = (BX+sign*0.27,local_y(5.92),BZ) if is_x else (BX,local_y(5.92),BZ+sign*0.19)
    arch_wall(bell, origin, 0.64 if not is_x else 0.46, 0.86,0.08,0.22,0.64,
              bottom=0.10,yaw=yaw,mat=0,segments=8)
# A small apricot bell and a lilac shadow ceiling inside the open belfry.
bell.lathe_ellipse([(0.07,0.07,0.0),(0.12,0.12,0.04),(0.10,0.10,0.17),(0.035,0.035,0.24)],
                   center=(BX,local_y(6.18),BZ),segments=12,mat=1,smooth=True)
bell.box((BX,local_y(6.76),BZ), (0.44,0.06,0.28), 3)
bell.box((BX,local_y(6.83),BZ), (0.74,0.10,0.50), 1)
bell.box((BX,local_y(6.90),BZ), (0.68,0.04,0.46), 0)
# Elliptical onion/hemisphere hybrid; smooth normals give a broad gradient without textures.
dome_profile = [
    (0.34,0.22,local_y(6.92)),
    (0.37,0.25,local_y(7.00)),
    (0.35,0.235,local_y(7.14)),
    (0.27,0.18,local_y(7.30)),
    (0.15,0.10,local_y(7.44)),
    (0.04,0.028,local_y(7.54)),
    (0.0,0.0,local_y(7.56)),
]
bell.lathe_ellipse(dome_profile,center=(BX,0,BZ),segments=20,mat=2,phase=math.pi/20,smooth=True)
bell.lathe_ellipse([(0.035,0.035,local_y(7.54)),(0.022,0.022,local_y(7.70)),
                    (0.012,0.012,local_y(8.10)),(0,0,local_y(8.14))],
                   center=(BX,0,BZ),segments=10,mat=1,smooth=True)
# The small triangular flag extends +X but remains left of X=-6.50 and behind Z=-4.08.
flag_points = [(BX+0.005,local_y(8.05),BZ),(BX+0.30,local_y(7.96),BZ+0.015),
               (BX+0.005,local_y(7.86),BZ)]
triangular_prism(bell,flag_points,0.010,1)
bell_obj = create_mesh("Bell_Pavilion_Dome_And_Flag",bell,
    [MATS["MV_Ivory"],MATS["MV_Apricot"],MATS["MV_DomeBlue"],MATS["MV_Lilac"]],bell_root,"03_BELL_TOWER")
bell_obj["footprint_world_x"] = [-7.28,-6.54]
bell_obj["footprint_world_z"] = [-4.58,-4.08]
bell_obj["top_world_y"] = 8.14

ASSETS = {
    "BReleaseTower_Body": (body_root,[core_obj,facade_obj]),
    "BReleaseTower_Platform": (platform_root,[slab_obj,rail_obj]),
    "BReleaseTower_BellTower": (bell_root,[bell_obj]),
}


def bounds(obj):
    points = [up(v.co) for v in obj.data.vertices]
    minimum = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    maximum = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    return minimum, maximum


def triangles(obj):
    return sum(len(p.vertices)-2 for p in obj.data.polygons)


# Source-level hard checks before any export.
core_min, core_max = bounds(core_obj)
assert (core_min-Vector((-0.885,0.48,-0.895))).length < 0.00001, core_min
assert (core_max-Vector((0.885,4.50,0.895))).length < 0.00001, core_max
slab_min, slab_max = bounds(slab_obj)
assert (slab_min-Vector((-1.685,4.31,-0.96))).length < 0.00001, slab_min
assert (slab_max-Vector((1.145,4.67,0.96))).length < 0.00001, slab_max
bell_min, bell_max = bounds(bell_obj)
assert bell_max.y+ANCHOR.y <= 8.14001 and bell_max.y+ANCHOR.y >= 8.1399
assert bell_min.x+ANCHOR.x >= -7.28001 and bell_max.x+ANCHOR.x <= -6.53999
assert bell_min.z+ANCHOR.z >= -4.58001 and bell_max.z+ANCHOR.z <= -4.07999
for root, meshes in ASSETS.values():
    assert root.location.length == 0
    assert len(meshes) <= 2
    for obj in meshes:
        assert obj.location.length == 0
        assert triangles(obj) <= 15000
        assert len(obj.data.materials) <= 4
        assert obj.data.uv_layers


def export_asset(name, root, meshes):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    kwargs = dict(filepath=str(FBX_DIR/(name+".fbx")),use_selection=True,object_types={"EMPTY","MESH"},
                  global_scale=1.0,apply_unit_scale=True,axis_forward="-Z",axis_up="Y",
                  use_mesh_modifiers=True,mesh_smooth_type="FACE",add_leaf_bones=False,
                  path_mode="AUTO",bake_anim=False)
    supported = bpy.ops.export_scene.fbx.get_rna_type().properties.keys()
    bpy.ops.export_scene.fbx(**{key:value for key,value in kwargs.items() if key in supported})


for name,(root,meshes) in ASSETS.items():
    export_asset(name,root,meshes)

# --- Source-only preview: ground, ball and a formal-view camera; never exported. ---
preview_mat = material("Preview_Ground", (0.50,0.45,0.66), 0.9)
ground = MeshBuilder()
ground.box((0,-0.06,0),(5.5,0.10,5.0),0)
# Direct preview object helper (no delivery root).
mesh = bpy.data.meshes.new("Preview_Ground_Mesh")
mesh.from_pydata([bp(v) for v in ground.vertices],[],ground.faces)
mesh.materials.append(preview_mat)
ground_obj = bpy.data.objects.new("Preview_Ground",mesh)
COLLECTIONS["90_PREVIEW_ONLY"].objects.link(ground_obj)
bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=12,radius=0.38,location=bp((-0.26,5.05,0)))
ball = bpy.context.object
ball.name = "Preview_Ball_Diameter_076"
for collection in list(ball.users_collection):
    collection.objects.unlink(ball)
COLLECTIONS["90_PREVIEW_ONLY"].objects.link(ball)
ball.data.materials.append(MATS["MV_Gold"])
for polygon in ball.data.polygons:
    polygon.use_smooth = True

world = bpy.data.worlds.new("BReleaseTower_Preview_World")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.30,0.38,0.48,1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.65
scene.world = world


def aim(obj,target):
    obj.rotation_euler = (bp(target)-obj.location).to_track_quat("-Z","Y").to_euler()


def area(name,position,target,energy,size,color):
    data = bpy.data.lights.new(name,"AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    data.color = color
    obj = bpy.data.objects.new(name,data)
    COLLECTIONS["90_PREVIEW_ONLY"].objects.link(obj)
    obj.location = bp(position)
    aim(obj,target)


area("Key",(3,9,-6),(0,3,0),1000,7,(1.0,0.90,0.80))
area("Fill",(-5,5,4),(0,3,0),700,6,(0.72,0.82,1.0))
sun_data = bpy.data.lights.new("Sun","SUN")
sun_data.energy = 1.8
sun_data.angle = math.radians(10)
sun = bpy.data.objects.new("Sun",sun_data)
COLLECTIONS["90_PREVIEW_ONLY"].objects.link(sun)
sun.location = bp((-3,9,-4))
aim(sun,(0,2,0))
cam_data = bpy.data.cameras.new("Formal_Tower_Preview")
cam_data.type = "ORTHO"
cam_data.ortho_scale = 9.7
cam = bpy.data.objects.new("Formal_Tower_Preview",cam_data)
COLLECTIONS["90_PREVIEW_ONLY"].objects.link(cam)
cam.location = bp((10,13,-13))
aim(cam,(-0.15,3.65,0))
scene.camera = cam
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 40
scene.cycles.use_denoising = True
scene.render.resolution_x = 1200
scene.render.resolution_y = 1400
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.view_transform = "AgX"
try:
    scene.view_settings.look = "AgX - Medium High Contrast"
except TypeError:
    pass
scene.render.filepath = str(PREVIEW_DIR/"BReleaseTower_Blender_Check.png")

bpy.ops.object.select_all(action="DESELECT")
for screen in bpy.data.screens:
    for area_ui in screen.areas:
        if area_ui.type == "VIEW_3D":
            area_ui.spaces.active.region_3d.view_perspective = "CAMERA"
            area_ui.spaces.active.shading.type = "MATERIAL"
blend_path = SOURCE_DIR/"BReleaseTower.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path),check_existing=False)
bpy.ops.render.render(write_still=True)

manifest = {
    "task":"B-ReleaseTower",
    "units":"metres",
    "anchor_world":list(ANCHOR),
    "pivot":"tower bottom centre",
    "unity_axes":"X right, +Y up, +Z forward",
    "source_blend":"ArtSource/Prototypes/B-ReleaseTower/BReleaseTower.blend",
    "contains_colliders":False,
    "external_textures":[],
    "alignment":{
        "tower_core_center_local":[0,2.49,0],"tower_core_size":[1.77,4.02,1.79],
        "launch_coping_center_local":[-0.27,4.49,0],"launch_coping_size":[2.83,0.36,1.92],
        "launch_top_world_y":4.45,"plinth_world_y":[-0.22,0.28],
    },
    "bell":{
        "centre_world_xz":[-6.91,-4.33],"allowed_footprint_world_x":[-7.3,-6.5],
        "allowed_footprint_world_z":[-4.6,-3.9],"actual_footprint_world_x":[-7.28,-6.54],
        "actual_footprint_world_z":[-4.58,-4.08],"top_world_y":8.14,
    },
    "modules":{},
}
for name,(root,meshes) in ASSETS.items():
    module = {"fbx":"FBX/"+name+".fbx","meshes":{},"triangles":0}
    for obj in meshes:
        minimum,maximum = bounds(obj)
        module["meshes"][obj.name] = {
            "triangles":triangles(obj),"vertices":len(obj.data.vertices),
            "materials":[mat.name for mat in obj.data.materials],
            "bounds_min_local":list(minimum),"bounds_max_local":list(maximum),
            "bounds_min_world":list(minimum+ANCHOR),"bounds_max_world":list(maximum+ANCHOR),
        }
        module["triangles"] += triangles(obj)
    manifest["modules"][name] = module
manifest["total_triangles"] = sum(module["triangles"] for module in manifest["modules"].values())
(DOC_DIR/"BReleaseTower_Dimensions.json").write_text(json.dumps(manifest,indent=2),encoding="utf-8")
print("B_RELEASE_TOWER_BUILD_COMPLETE",manifest["total_triangles"],"triangles")
for name,module in manifest["modules"].items():
    print(name,module["triangles"])
