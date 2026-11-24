"""Build three scalable Bounce Back K-02 guide ornaments.

Run from any working directory:
  blender.exe --background --python ArtSource/Kimi/K-02/build_k02_guide_trim.py

Authoring convention: Unity local +Y up, +Z out of the mounting surface.
Blender source uses +Z up and -Y out. Every mesh origin is the center of its
rear mounting plane. No wall, collider, animation, travel, or mechanism is made.
"""
from pathlib import Path
import bpy
import bmesh
import json
import math
import sys
from mathutils import Matrix, Vector

SOURCE_DIR = Path(__file__).resolve().parent
PROJECT_DIR = SOURCE_DIR.parents[2]
PISTON_REFERENCE = PROJECT_DIR / "ArtSource" / "Architectural Piston.blend"
PALETTE_REFERENCES = [
    PROJECT_DIR / "Assets" / "Scenes" / "Archive" / "BounceBack" / "Art" / "K-07" / "Materials" / "K07_White_Limestone.mat",
    PROJECT_DIR / "Assets" / "Scenes" / "Archive" / "BounceBack" / "Art" / "K-07" / "Materials" / "K07_Cobalt_Blue.mat",
]
DELIVERY_DIR = PROJECT_DIR / "Assets" / "Scenes" / "Archive" / "BounceBack" / "Art" / "K-02"
FBX_DIR = DELIVERY_DIR / "FBX"
PREVIEW_DIR = DELIVERY_DIR / "Preview"
DOC_DIR = DELIVERY_DIR / "Documentation"
for folder in (FBX_DIR, PREVIEW_DIR, DOC_DIR):
    folder.mkdir(parents=True, exist_ok=True)
LOG_FILE = (SOURCE_DIR / "blender-build.log").open("w", encoding="utf-8")
sys.stdout = LOG_FILE
sys.stderr = LOG_FILE

# Read the existing sources without saving them. The current piston source still
# contains its legacy pastel palette; K-07 supplies the requested white/cobalt.
def read_source_materials(path):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    result = []
    for mat in bpy.data.materials:
        if not mat.use_nodes:
            continue
        bsdf = next((node for node in mat.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
        if bsdf is None:
            continue
        result.append({
            "name": mat.name,
            "base_color": list(bsdf.inputs["Base Color"].default_value),
            "metallic": bsdf.inputs["Metallic"].default_value,
            "roughness": bsdf.inputs["Roughness"].default_value,
        })
    return result


piston_materials = read_source_materials(PISTON_REFERENCE)

# Values sampled from the named project URP materials. Keeping the values here makes
# this asset rebuild independent of other tasks' Blender files and working branches.
palette_reference = {
    "white_stone": {"name": "K07_White_Limestone", "base_color": [0.91, 0.885, 0.80, 1.0],
                    "metallic": 0.0, "roughness": 0.82},
    "cobalt_blue": {"name": "K07_Cobalt_Blue", "base_color": [0.025, 0.105, 0.52, 1.0],
                    "metallic": 0.0, "roughness": 0.56},
    # Muted brass requested for this ornament set; no brass slot exists in the legacy piston.
    "brass": {"name": "K02 muted brass (project gold hue, reduced brightness)",
              "base_color": [0.59, 0.38, 0.13, 1.0], "metallic": 0.72, "roughness": 0.38},
}
print("REFERENCE_PALETTE", json.dumps(palette_reference, indent=2))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.name = "K02_Guide_Trim_Three_Independent_Meshes"
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.unit_settings.length_unit = "METERS"
scene.frame_start = 1
scene.frame_end = 1
ornaments = bpy.data.collections.new("01_ORNAMENTS__THREE_MESHES")
studio = bpy.data.collections.new("90_STUDIO__NOT_EXPORTED")
scene.collection.children.link(ornaments)
scene.collection.children.link(studio)
C = Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def blender_point(point):
    return C @ Vector((*point, 1.0))


def make_material(name, reference, roughness, metallic, coat=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    color = tuple(reference["base_color"])
    mat.diffuse_color = color
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["IOR"].default_value = 1.48 if metallic == 0.0 else 1.5
    if "Coat Weight" in bsdf.inputs:
        bsdf.inputs["Coat Weight"].default_value = coat
        bsdf.inputs["Coat Roughness"].default_value = 0.18
    mat["reference_material"] = reference["name"]
    return mat


WHITE = make_material("K02_White_Stone", palette_reference["white_stone"], 0.80, 0.0)
BLUE = make_material("K02_Cobalt_Ceramic", palette_reference["cobalt_blue"], 0.22, 0.0, 0.32)
BRASS = make_material("K02_Brass", palette_reference["brass"], 0.38, 0.72, 0.05)


class MeshBuilder:
    def __init__(self):
        self.vertices = []
        self.faces = []

    def add(self, vertices, faces):
        offset = len(self.vertices)
        self.vertices.extend(tuple(v) for v in vertices)
        self.faces.extend(tuple(index + offset for index in face) for face in faces)

    def beveled_prism(self, center, size, corner=0.0, bevel=0.002):
        cx, cy, cz = center
        width, height, depth = size
        bevel = min(bevel, width * 0.1, height * 0.1, depth * 0.4)
        z0 = cz - depth * 0.5
        z1 = cz + depth * 0.5
        rings = (
            (z0, width - 2 * bevel, height - 2 * bevel, max(0.0001, corner - bevel)),
            (z0 + bevel, width, height, corner),
            (z1 - bevel, width, height, corner),
            (z1, width - 2 * bevel, height - 2 * bevel, max(0.0001, corner - bevel)),
        )
        vertices = []
        for z, w, h, c in rings:
            vertices.extend((cx + x, cy + y, z) for x, y in clipped_outline(w, h, c))
        faces = [tuple(reversed(range(8)))]
        for ring in range(3):
            first = ring * 8
            second = (ring + 1) * 8
            for i in range(8):
                n = (i + 1) % 8
                faces.append((first + i, first + n, second + n, second + i))
        faces.append(tuple(range(24, 32)))
        self.add(vertices, faces)

    def rectangular_ring(self, outer_size, inner_size, z0, z1, outer_corner, inner_corner):
        outer = clipped_outline(*outer_size, outer_corner)
        inner = clipped_outline(*inner_size, inner_corner)
        vertices = []
        for z, outline in ((z0, outer), (z0, inner), (z1, outer), (z1, inner)):
            vertices.extend((x, y, z) for x, y in outline)
        faces = []
        for i in range(8):
            n = (i + 1) % 8
            faces.append((n, i, 8 + i, 8 + n))
            faces.append((16 + i, 16 + n, 24 + n, 24 + i))
            faces.append((i, n, 16 + n, 16 + i))
            faces.append((8 + n, 8 + i, 24 + i, 24 + n))
        self.add(vertices, faces)

    def lathe(self, profile, segments, phase=0.0):
        vertices = []
        for radius, z in profile:
            for i in range(segments):
                angle = phase + math.tau * i / segments
                vertices.append((radius * math.cos(angle), radius * math.sin(angle), z))
        faces = [tuple(reversed(range(segments)))]
        for ring in range(len(profile) - 1):
            first = ring * segments
            second = (ring + 1) * segments
            for i in range(segments):
                n = (i + 1) % segments
                faces.append((first + i, first + n, second + n, second + i))
        faces.append(tuple(range((len(profile) - 1) * segments, len(profile) * segments)))
        self.add(vertices, faces)


def clipped_outline(width, height, corner):
    x = width * 0.5
    y = height * 0.5
    c = min(corner, x * 0.8, y * 0.8)
    return ((-x + c, -y), (x - c, -y), (x, -y + c), (x, y - c),
            (x - c, y), (-x + c, y), (-x, y - c), (-x, -y + c))


def part_object(name, builder, bevel=0.0):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata([blender_point(v).to_3d() for v in builder.vertices], [], builder.faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    ornaments.objects.link(obj)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    if bevel > 0.0:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        modifier = obj.modifiers.new("Small crafted edge", "BEVEL")
        modifier.width = bevel
        modifier.segments = 1
        modifier.limit_method = "ANGLE"
        modifier.angle_limit = math.radians(35)
        modifier.use_clamp_overlap = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj


def union_all(name, parts, material):
    target = parts[0]
    target.name = name
    for operand in parts[1:]:
        bpy.ops.object.select_all(action="DESELECT")
        target.select_set(True)
        bpy.context.view_layer.objects.active = target
        modifier = target.modifiers.new("Union decorative volume", "BOOLEAN")
        modifier.operation = "UNION"
        modifier.solver = "EXACT"
        modifier.object = operand
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        bpy.data.objects.remove(operand, do_unlink=True)
    target.data.materials.clear()
    target.data.materials.append(material)
    for polygon in target.data.polygons:
        polygon.material_index = 0
        polygon.use_smooth = False
    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    bm = bmesh.new()
    bm.from_mesh(target.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.000001)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=0.0000001)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(target.data)
    bm.free()
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    supported = bpy.ops.uv.smart_project.get_rna_type().properties.keys()
    options = dict(angle_limit=math.radians(66), island_margin=0.02,
                   area_weight=0.0, correct_aspect=True, scale_to_bounds=True)
    bpy.ops.uv.smart_project(**{key: value for key, value in options.items() if key in supported})
    bpy.ops.object.mode_set(mode="OBJECT")
    target.data.uv_layers.active.name = "UV0_SmartProject"
    bm = bmesh.new()
    bm.from_mesh(target.data)
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bm.to_mesh(target.data)
    bm.free()
    target.data.update()
    target["visual_only"] = True
    target["contains_colliders"] = False
    target["animation_or_travel"] = "None; Unity owner decides all transforms and motion"
    target["mount_plane"] = "Local Unity Z=0 / Blender Y=0"
    target["pivot"] = "Center of rear mounting face; local origin"
    target["outward_normal"] = "Unity +Z / Blender -Y"
    return target


# 01: a compact white-stone guide seat, not a full-height wall rail.
base = MeshBuilder()
base.beveled_prism((0, 0, 0.024), (0.300, 0.500, 0.048), 0.016, 0.004)
parts = [part_object("Stone back plate", base)]
for y in (-0.252, 0.252):
    cap = MeshBuilder()
    cap.beveled_prism((0, y, 0.036), (0.340, 0.056, 0.072), 0.009, 0.004)
    parts.append(part_object("Stepped end block", cap))
for x in (-0.102, 0.102):
    rail = MeshBuilder()
    rail.beveled_prism((x, 0, 0.079), (0.058, 0.456, 0.082), 0.009, 0.004)
    parts.append(part_object("Raised guide cheek", rail))
bed = MeshBuilder()
bed.beveled_prism((0, 0, 0.052), (0.112, 0.456, 0.018), 0.008, 0.002)
parts.append(part_object("Recessed center bed", bed))
stone = union_all("K02_White_Stone_Guide_Seat", parts, WHITE)

# 02: a glazed cobalt facing with an octagonal perimeter and three quiet flutes.
base = MeshBuilder()
base.beveled_prism((0, 0, 0.016), (0.260, 0.380, 0.032), 0.026, 0.004)
parts = [part_object("Ceramic mounting tile", base)]
border = MeshBuilder()
border.rectangular_ring((0.230, 0.340), (0.170, 0.264), 0.028, 0.050, 0.026, 0.020)
parts.append(part_object("Raised ceramic border", border, bevel=0.0015))
field = MeshBuilder()
field.beveled_prism((0, 0, 0.039), (0.158, 0.252, 0.018), 0.022, 0.002)
parts.append(part_object("Recessed octagonal field", field))
for x in (-0.034, 0.0, 0.034):
    flute = MeshBuilder()
    flute.beveled_prism((x, 0, 0.050), (0.016, 0.160, 0.014), 0.006, 0.002)
    parts.append(part_object("Ceramic flute", flute))
ceramic = union_all("K02_Cobalt_Ceramic_Slider_Facing", parts, BLUE)

# 03: a short brass axle cap; the shaft and its travel are deliberately absent.
outer = MeshBuilder()
outer.lathe(((0.072, 0.000), (0.092, 0.004), (0.100, 0.012),
             (0.100, 0.024), (0.092, 0.032), (0.092, 0.040),
             (0.078, 0.048), (0.074, 0.052)), 16, 0.0)
parts = [part_object("Sixteen-sided brass rim", outer)]
boss = MeshBuilder()
boss.lathe(((0.058, 0.040), (0.062, 0.046), (0.062, 0.056),
            (0.050, 0.066), (0.046, 0.080), (0.038, 0.086)), 8, 0.0)
parts.append(part_object("Octagonal axle boss", boss))
brass = union_all("K02_Brass_Axle_Cap", parts, BRASS)

ASSETS = {
    "white_stone_guide_seat": {
        "object": stone, "material": WHITE, "expected_dimensions_m": [0.340, 0.560, 0.120],
        "file": "FBX/K02_White_Stone_Guide_Seat.fbx", "prefab": "Prefabs/K02_White_Stone_Guide_Seat.prefab",
        "role_zh": "白石导轨座：低背板、双凸轨、阶梯端块；不是升降墙或完整行程导轨。",
    },
    "cobalt_ceramic_slider_facing": {
        "object": ceramic, "material": BLUE, "expected_dimensions_m": [0.260, 0.380, 0.057],
        "file": "FBX/K02_Cobalt_Ceramic_Slider_Facing.fbx", "prefab": "Prefabs/K02_Cobalt_Ceramic_Slider_Facing.prefab",
        "role_zh": "蓝瓷滑块饰面：八边形浅边框和三道浅凸棱；只负责表面装饰。",
    },
    "brass_axle_cap": {
        "object": brass, "material": BRASS, "expected_dimensions_m": [0.200, 0.200, 0.086],
        "file": "FBX/K02_Brass_Axle_Cap.fbx", "prefab": "Prefabs/K02_Brass_Axle_Cap.prefab",
        "role_zh": "黄铜轴帽：十六边外圈与八边轴头；不包含长轴、轴承或运动。",
    },
}


def unity_vertex(co):
    return Vector((co.x, co.z, -co.y))


def export_mesh(obj, path):
    original_location = obj.location.copy()
    original_rotation = obj.rotation_euler.copy()
    obj.location = (0, 0, 0)
    obj.rotation_euler = (0, 0, 0)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    kwargs = dict(filepath=str(path), use_selection=True, object_types={"MESH"},
                  global_scale=1.0, apply_unit_scale=True, axis_forward="-Z", axis_up="Y",
                  use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
                  path_mode="AUTO", bake_anim=False)
    supported = bpy.ops.export_scene.fbx.get_rna_type().properties.keys()
    bpy.ops.export_scene.fbx(**{key: value for key, value in kwargs.items() if key in supported})
    obj.location = original_location
    obj.rotation_euler = original_rotation


manifest = {
    "task": "K-02 / guide-trim subset",
    "blender_version": bpy.app.version_string,
    "source_blend": "ArtSource/Kimi/K-02/K02_Guide_Trim.blend",
    "units": "meters",
    "unity_axes": "+Y up; +Z outward from mounting plane; X across the ornament",
    "pivot": "Each mesh local origin is its rear mounting-face center",
    "visual_only": True,
    "mesh_object_count": 3,
    "external_textures": [],
    "contains_wall": False,
    "contains_animation": False,
    "contains_colliders": False,
    "assets": {},
}
for key, data in ASSETS.items():
    obj = data["object"]
    coordinates = [unity_vertex(v.co) for v in obj.data.vertices]
    minimum = [min(v[i] for v in coordinates) for i in range(3)]
    maximum = [max(v[i] for v in coordinates) for i in range(3)]
    dimensions = [maximum[i] - minimum[i] for i in range(3)]
    assert max(dimensions) <= 0.600001, (key, dimensions)
    assert abs(minimum[2]) <= 0.00001, (key, "Mounting plane is not Z=0", minimum)
    assert abs((minimum[0] + maximum[0]) * 0.5) <= 0.00001, (key, "X pivot")
    assert abs((minimum[1] + maximum[1]) * 0.5) <= 0.00001, (key, "Y pivot")
    for actual, expected in zip(dimensions, data["expected_dimensions_m"]):
        assert abs(actual - expected) <= 0.00002, (key, dimensions, data["expected_dimensions_m"])
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    non_manifold = sum(not edge.is_manifold for edge in bm.edges)
    loose_vertices = sum(not vertex.link_edges for vertex in bm.verts)
    degenerate_faces = sum(face.calc_area() < 0.00000000001 for face in bm.faces)
    bm.free()
    assert non_manifold == 0, (key, "non-manifold edges", non_manifold)
    assert loose_vertices == 0, (key, "loose vertices", loose_vertices)
    assert degenerate_faces == 0, (key, "degenerate faces", degenerate_faces)
    assert len(obj.data.uv_layers) == 1, (key, "UV layer count")
    output = DELIVERY_DIR / data["file"]
    export_mesh(obj, output)
    manifest["assets"][key] = {
        "object_name": obj.name,
        "file": data["file"], "prefab": data["prefab"], "role_zh": data["role_zh"],
        "local_dimensions_m_xyz": dimensions,
        "local_bounds_min_m_xyz": minimum, "local_bounds_max_m_xyz": maximum,
        "vertices": len(obj.data.vertices), "triangles": len(obj.data.polygons),
        "material": data["material"].name, "material_slots": len(obj.data.materials),
        "uv_layers": [layer.name for layer in obj.data.uv_layers],
        "non_manifold_edges": non_manifold, "loose_vertices": loose_vertices,
        "degenerate_faces": degenerate_faces, "fbx_bytes": output.stat().st_size,
    }

# Studio is non-geometry except for a temporary render floor removed before saving.
world = bpy.data.worlds.new("K02_Studio_World")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.68, 0.79, 0.88, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.38
scene.world = world


def area_light(name, location, target, energy, size, color):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    data.color = color
    obj = bpy.data.objects.new(name, data)
    studio.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


area_light("Studio_Key", (0.35, -0.75, 1.15), (-0.10, 0, 0), 105, 0.75, (1.0, 0.91, 0.78))
area_light("Studio_Fill", (-0.80, -0.28, 0.48), (-0.10, 0, 0), 48, 0.65, (0.74, 0.85, 1.0))
area_light("Studio_Rim", (0.10, 0.48, 0.95), (-0.10, 0, 0), 85, 0.55, (1.0, 0.96, 0.87))
camera_data = bpy.data.cameras.new("K02_Studio_Camera")
camera_data.type = "ORTHO"
camera_data.clip_start = 0.01
camera_data.clip_end = 100
camera = bpy.data.objects.new("K02_Studio_Camera", camera_data)
studio.objects.link(camera)
scene.camera = camera
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.render.resolution_percentage = 100
scene.render.engine = "BLENDER_EEVEE_NEXT"
if hasattr(scene, "eevee") and hasattr(scene.eevee, "taa_render_samples"):
    scene.eevee.taa_render_samples = 64
scene.view_settings.view_transform = "AgX"
try:
    scene.view_settings.look = "AgX - Medium High Contrast"
except TypeError:
    pass
floor_material = bpy.data.materials.new("Preview_Only_Background")
floor_material.use_nodes = True
floor_bsdf = floor_material.node_tree.nodes.get("Principled BSDF")
floor_bsdf.inputs["Base Color"].default_value = (0.46, 0.59, 0.65, 1)
floor_bsdf.inputs["Roughness"].default_value = 0.92


def point_camera(location, target, ortho_scale):
    camera.location = location
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = ortho_scale


def add_render_floor(z):
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, z))
    floor = bpy.context.object
    floor.name = "TEMP_RENDER_FLOOR"
    floor.data.materials.append(floor_material)
    return floor


def remove_render_floor(floor):
    mesh = floor.data
    bpy.data.objects.remove(floor, do_unlink=True)
    bpy.data.meshes.remove(mesh)


def render(path, width, height):
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


# Three separate objects, center-aligned only for this inspection view.
stone.location = (-0.42, 0, 0)
ceramic.location = (0, 0, 0)
brass.location = (0.34, -0.01, 0)
point_camera((0.48, -1.80, 0.72), (-0.10, -0.01, 0), 1.18)
floor = add_render_floor(-0.292)
render(PREVIEW_DIR / "K02_Guide_Trim_Three_Parts_Blender.png", 1600, 1000)
remove_render_floor(floor)
assert sum(obj.type == "MESH" for obj in scene.objects) == 3
scene["delivery_scope"] = "Exactly three scalable visual ornaments; no wall, motion, angle, travel or collision"
scene["palette_reference"] = "K-07 white/cobalt; custom muted brass; legacy pastel piston inspected but unchanged"
scene["preview_arrangement"] = "Side-by-side only; each FBX is exported independently at its own mounting pivot"
for screen in bpy.data.screens:
    for area_ui in screen.areas:
        if area_ui.type == "VIEW_3D":
            area_ui.spaces.active.region_3d.view_perspective = "CAMERA"
            area_ui.spaces.active.shading.type = "MATERIAL"
bpy.ops.object.select_all(action="DESELECT")
stone.select_set(True)
bpy.context.view_layer.objects.active = stone
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_DIR / "K02_Guide_Trim.blend"), check_existing=False)

# Optional assembly example, never saved into the source or delivered as a mechanism Prefab.
stone.location = (0, 0, 0)
ceramic.location = (0, -0.120, 0)
brass.location = (0, -0.177, 0)
point_camera((0.58, -1.25, 0.54), (0, -0.06, 0), 0.78)
floor = add_render_floor(-0.292)
render(PREVIEW_DIR / "K02_Assembled_Example_Blender.png", 1200, 1200)
remove_render_floor(floor)

# Flat palette JSON is consumed by the isolated Unity validation/build script.
def color_fields(mat):
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    color = bsdf.inputs["Base Color"].default_value
    return {
        "r": color[0], "g": color[1], "b": color[2],
        "metallic": bsdf.inputs["Metallic"].default_value,
        "roughness": bsdf.inputs["Roughness"].default_value,
        "coat": bsdf.inputs["Coat Weight"].default_value if "Coat Weight" in bsdf.inputs else 0.0,
    }


palette = {key: color_fields(mat) for key, mat in
           (("white_stone", WHITE), ("cobalt_blue", BLUE), ("brass", BRASS))}
(DOC_DIR / "K02_Palette.json").write_text(json.dumps(palette, indent=2), encoding="utf-8")
palette_provenance = {
    "piston_source": str(PISTON_REFERENCE.relative_to(PROJECT_DIR)),
    "piston_materials_at_inspection": piston_materials,
    "piston_has_requested_white_cobalt_brass_slots": False,
    "white_cobalt_reference": [str(path.relative_to(PROJECT_DIR)) for path in PALETTE_REFERENCES],
    "selected_palette": palette_reference,
    "note_zh": "旧活塞源文件仍为粉彩材质。按本次明确要求，白石与钴蓝沿用 K-07，黄铜使用压暗金色；未修改旧活塞。",
}
(DOC_DIR / "K02_Piston_Palette_Reference.json").write_text(
    json.dumps(palette_provenance, indent=2, ensure_ascii=False), encoding="utf-8")
(DOC_DIR / "K02_Dimensions.json").write_text(
    json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
print("K02_BUILD_COMPLETE", json.dumps(manifest, ensure_ascii=False))
