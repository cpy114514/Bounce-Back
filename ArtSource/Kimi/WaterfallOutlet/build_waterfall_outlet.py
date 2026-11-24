"""Build the Bounce Back waterfall outlet visual asset.

Run with Blender 5.x:
  blender.exe --background --python-exit-code 1 --python build_waterfall_outlet.py

Authored in Unity-style metres: X across, +Y up, +Z outflow.
The centre of the water tongue's top attachment edge is the shared origin.
Only the stone eave and water tongue are exported; cameras/lights are preview-only.
"""
from pathlib import Path
import json
import math
import bpy
import bmesh
from mathutils import Matrix, Vector

SOURCE_DIR = Path(__file__).resolve().parent
PROJECT_DIR = SOURCE_DIR.parents[2]
DELIVERY_DIR = PROJECT_DIR / "Assets" / "Scenes" / "Archive" / "BounceBack" / "Art" / "WaterfallOutlet"
FBX_DIR = DELIVERY_DIR / "FBX"
PREVIEW_DIR = DELIVERY_DIR / "Preview"
DOC_DIR = DELIVERY_DIR / "Documentation"
for folder in (FBX_DIR, PREVIEW_DIR, DOC_DIR):
    folder.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.unit_settings.length_unit = "METERS"

collections = {}
for name in ("01_DELIVERY", "90_PREVIEW_CAMERA_LIGHT"):
    collection = bpy.data.collections.new(name)
    scene.collection.children.link(collection)
    collections[name] = collection

# Unity (x,y,z) -> Blender (x,-z,y); a proper rotation, so winding is preserved.
C = Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
CI = C.inverted()


def to_blender(point):
    return (C @ Vector((*point, 1.0))).to_3d()


def to_unity(point):
    return (CI @ Vector((*point, 1.0))).to_3d()


def material(name, color, roughness=0.7, specular=0.25):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = (*color, 1.0)
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = 0.0
    shader.inputs["Specular IOR Level"].default_value = specular
    return mat


STONE_WHITE = material("Waterfall_White_Stone", (0.94, 0.92, 0.82), 0.82)
STONE_SHADE = material("Waterfall_White_Stone_Underside", (0.79, 0.80, 0.74), 0.88)
WATER_BASE = material("Waterfall_Pale_Blue", (0.34, 0.69, 0.82), 0.38, 0.32)
WATER_LIGHT = material("Waterfall_Pale_Blue_Light_Facet", (0.43, 0.77, 0.87), 0.42, 0.28)
WATER_SHADE = material("Waterfall_Pale_Blue_Shade_Facet", (0.27, 0.59, 0.74), 0.44, 0.28)
WATER_FOAM = material("Waterfall_Sparse_Foam_White", (0.80, 0.95, 0.98), 0.55, 0.20)


def create_root(name, pivot, notes):
    root = bpy.data.objects.new(name, None)
    collections["01_DELIVERY"].objects.link(root)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.18
    root["units"] = "metres; Unity X across, +Y up, +Z outflow"
    root["shared_origin"] = "Centre of water tongue top attachment edge / stone front-top lip"
    root["pivot"] = pivot
    root["visual_only"] = True
    root["contains_colliders"] = False
    root["notes"] = notes
    return root


def mesh_object(name, vertices_unity, faces, materials, material_indices, root):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata([to_blender(v) for v in vertices_unity], [], faces)
    mesh.update()
    for mat in materials:
        mesh.materials.append(mat)
    for polygon, index in zip(mesh.polygons, material_indices):
        polygon.material_index = index
        polygon.use_smooth = False
    obj = bpy.data.objects.new(name, mesh)
    collections["01_DELIVERY"].objects.link(obj)
    obj.parent = root
    obj["visual_only"] = True
    obj["contains_colliders"] = False
    return obj


def recalculate_closed_normals(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    result = {
        "boundary_edges": sum(1 for edge in bm.edges if edge.is_boundary),
        "non_manifold_edges": sum(1 for edge in bm.edges if not edge.is_manifold),
    }
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return result


def metric_box_uv(obj):
    mesh = obj.data
    uv = mesh.uv_layers.new(name="UV0_MetricBox")
    for polygon in mesh.polygons:
        dominant = max(range(3), key=lambda axis: abs(polygon.normal[axis]))
        axes = ((1, 2), (0, 2), (0, 1))[dominant]
        for loop_index in polygon.loop_indices:
            vertex = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            uv.data[loop_index].uv = (vertex[axes[0]], vertex[axes[1]])


# --- One closed, profiled white-stone eave. Front-top edge is Y=0, Z=0. ---
# Each pair is (Z, Y), walking around the slab's stepped/recessed cross-section.
profile = [
    (0.0, 0.0),
    (-0.70, 0.0),
    (-0.76, -0.06),
    (-0.76, -0.22),
    (-0.68, -0.30),
    (-0.10, -0.30),
    (0.0, -0.20),
    (0.0, -0.11),
    (-0.025, -0.095),
    (-0.025, -0.065),
    (0.0, -0.05),
]
count = len(profile)
stone_vertices = [(x, y, z) for x in (-0.80, 0.80) for z, y in profile]
stone_faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
stone_mats = [0, 0]
# Underside and recessed fascia use the second white-stone slot.
segment_materials = [0, 0, 0, 0, 1, 1, 1, 0, 1, 1, 0]
for index in range(count):
    nxt = (index + 1) % count
    stone_faces.append((index, nxt, nxt + count, index + count))
    stone_mats.append(segment_materials[index])
stone_root = create_root(
    "WaterfallOutlet_Stone_Eave",
    "Front-top lip centre; local (0,0,0)",
    "1.60 m nominal width. Closed stepped slab with a shallow fascia groove; no supports or collider.",
)
stone = mesh_object(
    "Stone_Eave",
    stone_vertices,
    stone_faces,
    [STONE_WHITE, STONE_SHADE],
    stone_mats,
    stone_root,
)
stone_topology = recalculate_closed_normals(stone)
metric_box_uv(stone)


# --- Water front surface: short feeder, rounded-over lip, then a faceted fall. ---
columns = (-0.5, -0.25, 0.0, 0.25, 0.5)
fold = (0.0, 0.024, -0.014, 0.020, 0.0)
# ring: Y, Z, full width, centre X, fold amount
rings = [
    (0.018, -0.54, 1.28, 0.0, 0.0),
    (0.018, -0.26, 1.34, 0.0, 0.0),
    (0.000, 0.000, 1.44, 0.0, 0.0),  # top attachment edge, centre exactly at shared origin
    (-0.040, 0.135, 1.44, 0.0, 0.65),
    (-0.155, 0.245, 1.42, 0.0, 1.0),
    (-0.400, 0.285, 1.40, 0.0, 1.0),
    (-0.820, 0.300, 1.37, -0.010, 1.0),
    (-1.280, 0.325, 1.34, 0.008, 1.0),
    (-1.760, 0.365, 1.31, 0.014, 1.0),
    (-2.200, 0.415, 1.28, 0.010, 1.0),
]


def ring_point(ring_index, column_index):
    y, z, width, center_x, fold_amount = rings[ring_index]
    return Vector((center_x + columns[column_index] * width, y, z + fold[column_index] * fold_amount))


def surface_point(s, u):
    """Interpolate the authored front surface. u is across the full width (-0.5..0.5)."""
    s = max(0.0, min(float(len(rings) - 1), s))
    lower = min(int(math.floor(s)), len(rings) - 2)
    blend = s - lower
    a = rings[lower]
    b = rings[lower + 1]
    y = a[0] * (1.0 - blend) + b[0] * blend
    z = a[1] * (1.0 - blend) + b[1] * blend
    width = a[2] * (1.0 - blend) + b[2] * blend
    center_x = a[3] * (1.0 - blend) + b[3] * blend
    fold_amount = a[4] * (1.0 - blend) + b[4] * blend
    u = max(-0.5, min(0.5, u))
    right = 0
    while right < len(columns) - 1 and columns[right] < u:
        right += 1
    right = max(1, min(len(columns) - 1, right))
    left = right - 1
    column_blend = (u - columns[left]) / (columns[right] - columns[left])
    folded = fold[left] * (1.0 - column_blend) + fold[right] * column_blend
    return Vector((center_x + u * width, y, z + folded * fold_amount))


def surface_normal(s, u):
    ds = 0.002
    du = 0.002
    along = surface_point(s + ds, u) - surface_point(s - ds, u)
    across = surface_point(s, u + du) - surface_point(s, u - du)
    normal = along.cross(across)
    return normal.normalized() if normal.length > 0.000001 else Vector((0, 0, 1))


water_vertices = [tuple(ring_point(row, column)) for row in range(len(rings)) for column in range(len(columns))]
water_faces = []
water_mats = []
# Alternating broad facets, kept subtle and opaque for simple URP/WebGL use.
band_materials = (0, 1, 0, 2)
for row in range(len(rings) - 1):
    for column in range(len(columns) - 1):
        a = row * len(columns) + column
        b = (row + 1) * len(columns) + column
        water_faces.append((a, b, b + 1, a + 1))
        material_index = band_materials[column]
        if row < 2 and column != 2:
            material_index = 0
        water_mats.append(material_index)

water_root = create_root(
    "WaterfallOutlet_Water_Tongue",
    "Top attachment edge centre; local (0,0,0)",
    "Independent closed thin water mesh with three shallow foam ribbons in the same mesh. Opaque pale blue; no animation or collider.",
)
water = mesh_object(
    "Water_Tongue",
    water_vertices,
    water_faces,
    [WATER_BASE, WATER_LIGHT, WATER_SHADE, WATER_FOAM],
    water_mats,
    water_root,
)
attachment_centre = surface_point(2.0, 0.0)
assert (attachment_centre - Vector((0, 0, 0))).length < 0.000001, attachment_centre

# UVs follow width and physical centreline distance, useful for later scrolling shaders.
uv = water.data.uv_layers.new(name="UV0_WaterFlow")
path_lengths = [0.0]
for first, second in zip(rings, rings[1:]):
    path_lengths.append(path_lengths[-1] + (Vector(second[:2]) - Vector(first[:2])).length)
for row in range(len(rings) - 1):
    for column in range(len(columns) - 1):
        face_index = row * (len(columns) - 1) + column
        polygon = water.data.polygons[face_index]
        for loop_index in polygon.loop_indices:
            vertex_index = water.data.loops[loop_index].vertex_index
            vertex_row = vertex_index // len(columns)
            vertex_column = vertex_index % len(columns)
            uv.data[loop_index].uv = (columns[vertex_column] + 0.5, path_lengths[vertex_row] / path_lengths[-1])

# Keep the visible front grid fixed; thickness grows into the stone/back of the fall.
bpy.context.view_layer.objects.active = water
water.select_set(True)
solidify = water.modifiers.new("Water_Thickness_26mm", "SOLIDIFY")
solidify.thickness = 0.026
solidify.offset = -1.0
solidify.use_even_offset = False
bpy.ops.object.modifier_apply(modifier=solidify.name)
water.select_set(False)
water_base_topology = recalculate_closed_normals(water)
assert water_base_topology["boundary_edges"] == 0, water_base_topology

# Three sparse, shallow foam ribbons. They are joined into the water mesh, not extra objects.
# Dense path sampling follows every bend; tapered ends avoid rectangular raised-looking caps.
ribbon_specs = [
    ("Lip_And_Upper", 0.030, [(0.85, -0.27), (2.0, -0.27), (4.65, -0.26), (5.95, -0.24)]),
    ("Right_Middle", 0.036, [(4.05, 0.26), (5.35, 0.25), (7.35, 0.23)]),
    ("Lower_Centre", 0.025, [(6.65, -0.03), (7.75, -0.05), (8.85, -0.07)]),
]


def ribbon_u(keys, s):
    for first, second in zip(keys, keys[1:]):
        if first[0] <= s <= second[0]:
            blend = (s - first[0]) / (second[0] - first[0])
            return first[1] * (1.0 - blend) + second[1] * blend
    return keys[0][1] if s < keys[0][0] else keys[-1][1]


ribbon_vertices = []
ribbon_faces = []
ribbon_uvs = []
for _, width, keys in ribbon_specs:
    start = len(ribbon_vertices)
    first_s, last_s = keys[0][0], keys[-1][0]
    samples = {first_s, last_s}
    sample_index = math.ceil(first_s * 4.0)
    while sample_index / 4.0 < last_s:
        samples.add(sample_index / 4.0)
        sample_index += 1
    nodes = []
    for s in sorted(samples):
        progress = (s - first_s) / (last_s - first_s)
        taper = min(1.0, 0.12 + 4.0 * progress, 0.12 + 4.0 * (1.0 - progress))
        nodes.append((s, ribbon_u(keys, s), taper))
    for s, u, taper in nodes:
        lower = min(int(s), len(rings) - 2)
        blend = s - lower
        current_width = rings[lower][2] * (1.0 - blend) + rings[lower + 1][2] * blend
        half_u = min(0.035, (width * taper * 0.5) / current_width)
        for side in (-1.0, 1.0):
            side_u = u + side * half_u
            point = surface_point(s, side_u) + surface_normal(s, side_u) * 0.0025
            ribbon_vertices.append(tuple(point))
            ribbon_uvs.append((0.0 if side < 0 else 1.0, s / (len(rings) - 1)))
    for index in range(len(nodes) - 1):
        a = start + index * 2
        ribbon_faces.append((a, a + 2, a + 3, a + 1))

ribbons = mesh_object(
    "Temporary_Foam_Ribbons",
    ribbon_vertices,
    ribbon_faces,
    [WATER_FOAM],
    [0] * len(ribbon_faces),
    water_root,
)
ribbon_uv = ribbons.data.uv_layers.new(name="UV0_WaterFlow")
for polygon in ribbons.data.polygons:
    for loop_index in polygon.loop_indices:
        vertex_index = ribbons.data.loops[loop_index].vertex_index
        ribbon_uv.data[loop_index].uv = ribbon_uvs[vertex_index]

bpy.ops.object.select_all(action="DESELECT")
water.select_set(True)
ribbons.select_set(True)
bpy.context.view_layer.objects.active = water
bpy.ops.object.join()
water.name = "Water_Tongue"
water.data.name = "Water_Tongue_Mesh"
for polygon in water.data.polygons:
    polygon.use_smooth = False
water["foam_ribbons"] = 3
water["base_thickness_m"] = 0.026
water["top_attachment_width_m"] = 1.44
water["drop_from_origin_m"] = 2.20
water["top_attached_edge_center_unity"] = [0.0, 0.0, 0.0]
water["flow_direction"] = "Feeder +Z, then down -Y"
stone["nominal_width_m"] = 1.60
stone["slab_depth_m"] = 0.76
stone["slab_height_m"] = 0.30

# Validate source geometry before export.
stone.data.validate(verbose=True)
water.data.validate(verbose=True)
assert stone_topology["boundary_edges"] == 0, stone_topology
assert len([obj for obj in collections["01_DELIVERY"].objects if obj.type == "MESH"]) == 2
assert stone.data != water.data
assert stone.location.length == 0 and water.location.length == 0
assert stone_root.location.length == 0 and water_root.location.length == 0


def bounds_unity(obj):
    points = [to_unity(vertex.co) for vertex in obj.data.vertices]
    minimum = [min(point[axis] for point in points) for axis in range(3)]
    maximum = [max(point[axis] for point in points) for axis in range(3)]
    dimensions = [maximum[axis] - minimum[axis] for axis in range(3)]
    return minimum, maximum, dimensions


stone_min, stone_max, stone_dimensions = bounds_unity(stone)
water_min, water_max, water_dimensions = bounds_unity(water)
assert abs(stone_dimensions[0] - 1.60) < 0.00001, stone_dimensions
assert abs(stone_max[1]) < 0.00001 and abs(stone_max[2]) < 0.00001, stone_max
# The feeder skin is at Y=0.018; its 2.5 mm foam overlay raises the overall maximum to 0.0205.
assert 0.0179 <= water_max[1] <= 0.0206, water_max


def export_fbx(root, mesh, filename):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    kwargs = dict(
        filepath=str(FBX_DIR / filename),
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        path_mode="AUTO",
        bake_anim=False,
    )
    supported = bpy.ops.export_scene.fbx.get_rna_type().properties.keys()
    bpy.ops.export_scene.fbx(**{key: value for key, value in kwargs.items() if key in supported})


export_fbx(stone_root, stone, "WaterfallOutlet_Stone_Eave.fbx")
export_fbx(water_root, water, "WaterfallOutlet_Water_Tongue.fbx")

# A convenient assembly with the same two independent meshes, both at the shared origin.
bpy.ops.object.select_all(action="DESELECT")
for obj in (stone_root, stone, water_root, water):
    obj.select_set(True)
bpy.context.view_layer.objects.active = stone
kwargs = dict(
    filepath=str(FBX_DIR / "WaterfallOutlet_Assembly.fbx"),
    use_selection=True,
    object_types={"EMPTY", "MESH"},
    global_scale=1.0,
    apply_unit_scale=True,
    axis_forward="-Z",
    axis_up="Y",
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    path_mode="AUTO",
    bake_anim=False,
)
supported = bpy.ops.export_scene.fbx.get_rna_type().properties.keys()
bpy.ops.export_scene.fbx(**{key: value for key, value in kwargs.items() if key in supported})


# --- Isometric preview, including a closer lip view. ---
def make_camera(name, position, target, full_height):
    data = bpy.data.cameras.new(name)
    data.type = "ORTHO"
    data.sensor_fit = "HORIZONTAL"
    data.ortho_scale = full_height
    data.clip_start = 0.05
    data.clip_end = 100.0
    obj = bpy.data.objects.new(name, data)
    collections["90_PREVIEW_CAMERA_LIGHT"].objects.link(obj)
    obj.location = to_blender(position)
    direction = (to_blender(target) - obj.location).normalized()
    right = direction.cross(Vector((0, 0, 1))).normalized()
    up = right.cross(direction).normalized()
    backward = -direction
    orientation = Matrix(((right.x, up.x, backward.x),
                          (right.y, up.y, backward.y),
                          (right.z, up.z, backward.z)))
    obj.rotation_euler = orientation.to_euler()
    obj["unity_position"] = list(position)
    obj["unity_target"] = list(target)
    return obj


iso_camera = make_camera("Isometric_Outflow", (4.0, 2.45, 5.0), (0.0, -0.88, -0.03), 3.35)
detail_camera = make_camera("Isometric_Lip_Detail", (3.2, 1.65, 4.2), (0.0, -0.22, -0.05), 1.45)
world = bpy.data.worlds.new("Waterfall_Preview_World")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.16, 0.23, 0.28, 1.0)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.65
scene.world = world


def area_light(name, position, target, energy, size, color):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    data.color = color
    obj = bpy.data.objects.new(name, data)
    collections["90_PREVIEW_CAMERA_LIGHT"].objects.link(obj)
    obj.location = to_blender(position)
    obj.rotation_euler = (to_blender(target) - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


area_light("Key_Softbox", (3.5, 5.0, 4.0), (0.0, -0.8, 0.0), 650.0, 5.0, (1.0, 0.93, 0.82))
area_light("Cool_Fill", (-4.0, 1.5, 2.0), (0.0, -0.9, 0.1), 350.0, 4.0, (0.72, 0.86, 1.0))
sun_data = bpy.data.lights.new("Preview_Sun", "SUN")
sun_data.energy = 1.5
sun_data.angle = math.radians(12.0)
sun = bpy.data.objects.new("Preview_Sun", sun_data)
collections["90_PREVIEW_CAMERA_LIGHT"].objects.link(sun)
sun.location = to_blender((-3.0, 6.0, 4.0))
sun.rotation_euler = (to_blender((0.0, -0.8, 0.0)) - sun.location).to_track_quat("-Z", "Y").to_euler()

scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 40
scene.cycles.use_denoising = True
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = "AgX"
try:
    scene.view_settings.look = "AgX - Medium High Contrast"
except TypeError:
    pass


def render(camera, filename, width, height, full_height):
    scene.camera = camera
    camera.data.ortho_scale = full_height * (width / height)
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.filepath = str(PREVIEW_DIR / filename)
    bpy.ops.render.render(write_still=True)


# Save an uncluttered editable source with only the two delivery meshes in that collection.
bpy.ops.object.select_all(action="DESELECT")
scene.camera = iso_camera
scene.render.resolution_x = 1200
scene.render.resolution_y = 1200
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == "VIEW_3D":
            area.spaces.active.region_3d.view_perspective = "CAMERA"
            area.spaces.active.shading.type = "MATERIAL"
blend_path = SOURCE_DIR / "WaterfallOutlet.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)

render(iso_camera, "WaterfallOutlet_Isometric.png", 1200, 1200, 3.35)
render(detail_camera, "WaterfallOutlet_Lip_Detail.png", 1200, 900, 1.45)
scene.camera = iso_camera
scene.render.resolution_x = 1200
scene.render.resolution_y = 1200
iso_camera.data.ortho_scale = 3.35
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)


def triangle_count(mesh):
    return sum(len(polygon.vertices) - 2 for polygon in mesh.polygons)


manifest = {
    "asset": "WaterfallOutlet",
    "units": "metres",
    "unity_axes": "X across; +Y up; +Z outflow",
    "shared_origin_unity": [0.0, 0.0, 0.0],
    "origin_definition": "Centre of the water tongue top attachment edge, coincident with the stone front-top lip",
    "nominal_width_m": 1.60,
    "visual_only": True,
    "contains_colliders": False,
    "external_textures": [],
    "source_blend": "ArtSource/Kimi/WaterfallOutlet/WaterfallOutlet.blend",
    "rebuild_script": "ArtSource/Kimi/WaterfallOutlet/build_waterfall_outlet.py",
    "stone": {
        "fbx": "FBX/WaterfallOutlet_Stone_Eave.fbx",
        "bounds_min_unity": stone_min,
        "bounds_max_unity": stone_max,
        "dimensions_unity": stone_dimensions,
        "vertices": len(stone.data.vertices),
        "triangles": triangle_count(stone.data),
        "closed_manifold": stone_topology["non_manifold_edges"] == 0,
        "materials": [STONE_WHITE.name, STONE_SHADE.name],
    },
    "water": {
        "fbx": "FBX/WaterfallOutlet_Water_Tongue.fbx",
        "bounds_min_unity": water_min,
        "bounds_max_unity": water_max,
        "dimensions_unity": water_dimensions,
        "top_attachment_width_m": 1.44,
        "drop_from_origin_m": 2.20,
        "base_thickness_m": 0.026,
        "foam_ribbons": 3,
        "vertices": len(water.data.vertices),
        "triangles": triangle_count(water.data),
        "closed_base_before_foam_overlays": water_base_topology["non_manifold_edges"] == 0,
        "materials": [WATER_BASE.name, WATER_LIGHT.name, WATER_SHADE.name, WATER_FOAM.name],
    },
    "assembly_fbx": "FBX/WaterfallOutlet_Assembly.fbx",
    "previews": ["Preview/WaterfallOutlet_Isometric.png", "Preview/WaterfallOutlet_Lip_Detail.png"],
}
(DOC_DIR / "WaterfallOutlet_Dimensions.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
print("WATERFALL_OUTLET_BUILD_COMPLETE")
print(json.dumps({"stone_dimensions": stone_dimensions, "water_dimensions": water_dimensions,
                  "stone_triangles": triangle_count(stone.data), "water_triangles": triangle_count(water.data)}, indent=2))
