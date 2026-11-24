"""Build A-FloatingIsland as one static, metre-scale Unity visual mesh.

Run:
  blender --background --python-exit-code 1 --python ArtSource/Prototypes/A-FloatingIsland/build_floating_island.py
  ... -- --no-render

Authoring coordinates are Unity local: +Y up, X across, Z depth.
The top mounting plane is local Y=0 and the pivot is its centre.
All geometry is baked into Blender through X=x, Y=-z, Z=y.
"""
from pathlib import Path
import bpy
import bmesh
import json
import math
import random
import sys
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

TASK = "A-FloatingIsland"
ROOT = Path(__file__).resolve().parents[3]
SOURCE = Path(__file__).resolve().parent
DELIVERY = ROOT / "Assets" / "Scenes" / "Archive" / "BounceBack" / "Art" / TASK
for folder in (SOURCE, DELIVERY / "FBX", DELIVERY / "Materials",
               DELIVERY / "Prefabs", DELIVERY / "Preview", DELIVERY / "Documentation"):
    folder.mkdir(parents=True, exist_ok=True)

NO_RENDER = "--no-render" in sys.argv
ANCHOR = Vector((0.0, -0.22, 0.05))
# The layout JSON has no foundation box. These values are read-only facts from
# SantoriniSystemsAuthoring.cs: Foundation Crown, centred on deck z=0.05.
FOUNDATION_HALF_X = 12.36 * 0.5
FOUNDATION_HALF_Z = 9.83 * 0.5
FOUNDATION_AUTHORING_TOP_WORLD = -0.225
REQUESTED_TOP_WORLD = -0.22
WATERFALL_WORLD = Vector((5.72, 0.43, 5.24))
EAST_ROUTE_WORLD = Vector((9.7, 0.86, 3.83))

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for collection in list(bpy.data.collections):
    if collection.name == "Collection":
        bpy.data.collections.remove(collection)
scene = bpy.context.scene
bpy.context.preferences.filepaths.save_version = 0
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.unit_settings.length_unit = "METERS"
asset_collection = bpy.data.collections.new("01_A_FLOATING_ISLAND")
scene.collection.children.link(asset_collection)
preview_collection = bpy.data.collections.new("90_SOURCE_PREVIEW")
scene.collection.children.link(preview_collection)

# Baked Unity-to-Blender basis change. Export uses standard -Z forward / Y up.
C = Matrix(((1.0, 0.0, 0.0, 0.0),
            (0.0, 0.0, -1.0, 0.0),
            (0.0, 1.0, 0.0, 0.0),
            (0.0, 0.0, 0.0, 1.0)))


def B(point):
    return (C @ Vector((*point, 1.0))).to_3d()


def U(point):
    return (C.inverted() @ Vector((*point, 1.0))).to_3d()


def linear_channel(value):
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


def make_material(name, srgb, roughness):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = (*srgb, 1.0)
    material["palette_color_space"] = "sRGB"
    material["palette_srgb"] = list(srgb)
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = (*[linear_channel(v) for v in srgb], 1.0)
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Metallic"].default_value = 0.0
    return material


IVORY = make_material("MV_Ivory", (0.95, 0.88, 0.77), 0.84)
BLUE = make_material("MV_DomeBlue", (0.30, 0.47, 0.80), 0.68)
ROSE = make_material("MV_Rose", (0.74, 0.45, 0.47), 0.88)
DEEP = make_material("MV_RoseDeep", (0.55, 0.30, 0.38), 0.92)
PALETTE = [IVORY, BLUE, ROSE, DEEP]
parts = []


def recalc(mesh):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def mesh_object(name, vertices, faces, material, collection=None, is_part=True):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata([B(v) for v in vertices], [], faces)
    mesh.update()
    recalc(mesh)
    if material:
        mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    (collection or asset_collection).objects.link(obj)
    if is_part:
        parts.append(obj)
    return obj


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_modifier(obj, modifier):
    activate(obj)
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def finish_surface(obj, bevel_width=0.0, segments=2):
    if bevel_width > 0.0:
        bevel = obj.modifiers.new("Small architectural edge catches", "BEVEL")
        bevel.width = bevel_width
        bevel.segments = segments
        bevel.limit_method = "ANGLE"
        bevel.angle_limit = math.radians(30.0)
        bevel.harden_normals = True
        bevel.use_clamp_overlap = True
        apply_modifier(obj, bevel)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    for edge in bm.edges:
        if edge.is_manifold and edge.calc_face_angle() > math.radians(60.0):
            edge.smooth = False
    bm.to_mesh(obj.data)
    bm.free()
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    weighted = obj.modifiers.new("Area weighted large-plane normals", "WEIGHTED_NORMAL")
    weighted.keep_sharp = True
    weighted.weight = 50
    apply_modifier(obj, weighted)


def clipped_outline(half_x, half_z, chamfer):
    c = min(chamfer, half_x * 0.45, half_z * 0.45)
    return [(-half_x + c, -half_z), (half_x - c, -half_z),
            (half_x, -half_z + c), (half_x, half_z - c),
            (half_x - c, half_z), (-half_x + c, half_z),
            (-half_x, half_z - c), (-half_x, -half_z + c)]


def prism_from_rings(name, bottom, top, material, finish=0.0):
    count = len(bottom)
    assert count == len(top)
    vertices = list(bottom) + list(top)
    faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
    for i in range(count):
        j = (i + 1) % count
        faces.append((i, j, count + j, count + i))
    obj = mesh_object(name, vertices, faces, material)
    if finish:
        finish_surface(obj, finish)
    return obj


def rectangular_layer(name, y_top, height, overhang, material, chamfer=0.38, bevel=0.018):
    outline = clipped_outline(FOUNDATION_HALF_X + overhang,
                              FOUNDATION_HALF_Z + overhang, chamfer)
    bottom = [(x, y_top - height, z) for x, z in outline]
    top = [(x, y_top, z) for x, z in outline]
    return prism_from_rings(name, bottom, top, material), outline


def box_part(name, center, size, material, bevel=0.0, is_part=True):
    x, y, z = center
    hx, hy, hz = (v * 0.5 for v in size)
    vertices = [(x - hx, y - hy, z - hz), (x + hx, y - hy, z - hz),
                (x + hx, y - hy, z + hz), (x - hx, y - hy, z + hz),
                (x - hx, y + hy, z - hz), (x + hx, y + hy, z - hz),
                (x + hx, y + hy, z + hz), (x - hx, y + hy, z + hz)]
    faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
             (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    obj = mesh_object(name, vertices, faces, material, is_part=is_part)
    if bevel:
        finish_surface(obj, bevel)
    return obj


def arch_profile(center_u, bottom, spring, radius, segments=12):
    points = [(center_u - radius, bottom), (center_u + radius, bottom)]
    for i in range(segments + 1):
        angle = math.pi * i / segments
        points.append((center_u + radius * math.cos(angle),
                       spring + radius * math.sin(angle)))
    return points


def oriented_arch(name, face, center_u, bottom, spring, radius,
                   depth_front, depth_back, material=None, collection=None, is_part=True):
    profile = arch_profile(center_u, bottom, spring, radius)
    vertices = []
    for depth in (depth_front, depth_back):
        for u, y in profile:
            if face == "-Z":
                vertices.append((u, y, depth))
            elif face == "+X":
                vertices.append((depth, y, u))
            else:
                raise ValueError(face)
    n = len(profile)
    faces = [tuple(reversed(range(n))), tuple(range(n, n * 2))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    return mesh_object(name, vertices, faces, material,
                       collection=collection, is_part=is_part)


def difference(target, cutter):
    modifier = target.modifiers.new("Cut " + cutter.name, "BOOLEAN")
    modifier.operation = "DIFFERENCE"
    modifier.solver = "EXACT"
    modifier.object = cutter
    apply_modifier(target, modifier)
    bpy.data.objects.remove(cutter, do_unlink=True)


# Four successively inset architectural courses: 0.65, 0.85, 0.80, 0.85 m.
layers = []
layer_specs = [
    ("01_Ivory_Crown", 0.0, 0.65, 0.58, IVORY),
    ("02_Ivory_Window_Course", -0.65, 0.85, 0.42, IVORY),
    ("03_Rose_Step", -1.50, 0.80, 0.26, ROSE),
    ("04_Deep_Step", -2.30, 0.85, 0.10, DEEP),
]
for name, top, height, overhang, material in layer_specs:
    layer, _ = rectangular_layer(name, top, height, overhang, material)
    layers.append(layer)

# Four genuine blind arched recesses with smaller cobalt backs.
window_records = []
window_bottom = -1.39
window_spring = -1.03
for face, center_u in (("-Z", -3.25), ("-Z", 0.15),
                       ("+X", -2.55), ("+X", 0.15)):
    if face == "-Z":
        outer = -(FOUNDATION_HALF_Z + 0.42)
        cut_front, cut_back = outer - 0.12, outer + 0.32
        panel_front, panel_back = outer + 0.285, outer + 0.315
    else:
        outer = FOUNDATION_HALF_X + 0.42
        cut_front, cut_back = outer + 0.12, outer - 0.32
        panel_front, panel_back = outer - 0.285, outer - 0.315
    cutter = oriented_arch("TEMP_Arch_Cutter", face, center_u, window_bottom,
                            window_spring, 0.30, cut_front, cut_back,
                            collection=preview_collection, is_part=False)
    difference(layers[1], cutter)
    panel = oriented_arch("Cobalt_Recess_" + face + "_" + str(center_u), face,
                           center_u, window_bottom + 0.035, window_spring - 0.015,
                           0.255, panel_front, panel_back, material=BLUE)
    finish_surface(panel, 0.004, 1)
    window_records.append({"face": face, "center_u_local": center_u,
                           "cut_radius": 0.30, "bottom_local_y": window_bottom,
                           "spring_local_y": window_spring, "recess_depth": 0.32})

# Waterfall groove: its back is exactly x=5.72; its z range is wholly outside
# the authored foundation's z=4.965 edge, so the original deck remains intact.
channel_local_z = WATERFALL_WORLD.z - ANCHOR.z
# Cut through the full expanded +X/+Z silhouette, never leave a thin outer wall.
# The z=4.99 inner edge is 0.075 m outside the authored crown's z=4.915.
channel_center_z = (4.99 + 6.20) * 0.5
channel_center_y = (-11.50 + 0.75) * 0.5
channel_size = (8.20 - 5.72, 0.75 - (-11.50), 6.20 - 4.99)
channel = box_part("TEMP_Waterfall_Channel",
                   ((5.72 + 8.2) * 0.5, channel_center_y, channel_center_z),
                   channel_size, None, is_part=False)
difference(layers[0], channel)
# Recreate the temporary solid for each remaining target.
for target in layers[1:]:
    cutter = box_part("TEMP_Waterfall_Channel",
                      ((5.72 + 8.2) * 0.5, channel_center_y, channel_center_z),
                      channel_size, None, is_part=False)
    difference(target, cutter)
for layer, spec in zip(layers, layer_specs):
    finish_surface(layer, 0.018 if spec[0] != "01_Ivory_Crown" else 0.026, 2)

# A compact +X balcony with broad parapets and two triangular corbels.
wall_x = FOUNDATION_HALF_X + 0.42
balcony_z = 0.15
box_part("Balcony_Floor", (wall_x + 0.41, -1.50, balcony_z),
         (0.94, 0.16, 1.62), IVORY, 0.018)
box_part("Balcony_Outer_Parapet", (wall_x + 0.80, -1.25, balcony_z),
         (0.14, 0.34, 1.62), IVORY, 0.012)
for z in (balcony_z - 0.74, balcony_z + 0.74):
    box_part("Balcony_Side_Parapet", (wall_x + 0.42, -1.25, z),
             (0.82, 0.34, 0.14), IVORY, 0.012)


def corbel(name, z_center):
    profile = [(wall_x - 0.14, -2.08), (wall_x - 0.14, -1.58),
               (wall_x + 0.66, -1.58)]
    vertices = []
    for z in (z_center - 0.09, z_center + 0.09):
        vertices.extend((x, y, z) for x, y in profile)
    faces = [(2, 1, 0), (3, 4, 5), (0, 1, 4, 3),
             (1, 2, 5, 4), (2, 0, 3, 5)]
    obj = mesh_object(name, vertices, faces, ROSE)
    finish_surface(obj, 0.008, 1)


corbel("Balcony_Corbel_A", balcony_z - 0.48)
corbel("Balcony_Corbel_B", balcony_z + 0.48)

# Irregular tapering rock. The first ring exactly matches the lowest masonry.
rng = random.Random(271828)
ring_specs = [
    (-3.15, 6.28, 5.015, 0.00, 0.00),
    (-4.10, 5.75, 4.45, -0.10, 0.04),
    (-5.10, 5.05, 3.85, 0.16, -0.10),
    (-6.10, 4.40, 3.30, -0.24, 0.16),
    (-7.15, 3.65, 2.70, -0.02, 0.02),
    (-8.25, 2.75, 1.95, -0.38, 0.24),
    (-9.45, 1.55, 1.10, -0.24, 0.42),
]
rings = []
for index, (y, hx, hz, cx, cz) in enumerate(ring_specs):
    chamfer = 0.38 if index == 0 else min(0.42, hx * 0.18, hz * 0.18)
    outline = clipped_outline(hx, hz, chamfer)
    ring = []
    for x, z in outline:
        jitter = 0.0 if index == 0 else min(0.24, 0.07 + index * 0.025)
        ring.append((x + cx + rng.uniform(-jitter, jitter), y,
                     z + cz + rng.uniform(-jitter, jitter)))
    rings.append(ring)
rock_bands = []
for index in range(len(rings) - 1):
    band = prism_from_rings("Rock_Stratum_%02d" % (index + 1),
                            rings[index + 1], rings[index],
                            ROSE if index % 2 == 0 else DEEP)
    rock_bands.append(band)


def hanging_tip(name, base_ring, apex, material):
    count = len(base_ring)
    vertices = list(base_ring) + [apex]
    faces = [tuple(reversed(range(count)))]
    for i in range(count):
        faces.append((i, (i + 1) % count, count))
    return mesh_object(name, vertices, faces, material)


hanging_tip("Pendant_Central", rings[-1], (-0.52, -11.15, 0.52), DEEP)
for index, (y, hx, hz, cx, cz, apex, material) in enumerate([
    (-6.90, 0.68, 0.58, 2.42, -0.62, (2.18, -9.35, -0.42), ROSE),
    (-6.80, 0.60, 0.55, -2.68, 1.25, (-2.88, -8.85, 1.48), DEEP),
]):
    outline = clipped_outline(hx, hz, 0.12)
    base = [(x + cx, y, z + cz) for x, z in outline]
    hanging_tip("Pendant_Side_%d" % (index + 1), base, apex, material)

# Apply the same upper waterfall groove to any rock band it intersects.
for band in rock_bands[:2]:
    cutter = box_part("TEMP_Rock_Channel",
                      ((5.72 + 8.2) * 0.5, channel_center_y, channel_center_z),
                      channel_size, None, is_part=False)
    difference(band, cutter)

# Join closed visual solids without welding overlapping volumes.
bpy.ops.object.select_all(action="DESELECT")
for obj in parts:
    obj.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
island = bpy.context.object
island.name = "A_FloatingIsland"
island.data.name = "A_FloatingIsland_Mesh"
activate(island)
recalc(island.data)
# Normalize slot order without changing the semantic material on any face.
joined_slot_names = [material.name for material in island.data.materials]
face_material_names = [joined_slot_names[polygon.material_index]
                       for polygon in island.data.polygons]
island.data.materials.clear()
for material in PALETTE:
    island.data.materials.append(material)
palette_indices = {material.name: index for index, material in enumerate(PALETTE)}
for polygon, material_name in zip(island.data.polygons, face_material_names):
    polygon.material_index = palette_indices[material_name]

# Smart UVs are provided for later integration; no texture assets are required.
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.012,
                         area_weight=0.0, correct_aspect=True, scale_to_bounds=True)
bpy.ops.object.mode_set(mode="OBJECT")
island.data.uv_layers.active.name = "UV0_SmartProject"
triangulate = island.modifiers.new("WebGL triangles (non-destructive)", "TRIANGULATE")
triangulate.keep_custom_normals = True
island["task"] = TASK
island["visual_only"] = True
island["pivot"] = "Top mounting plane centre; Unity local (0,0,0)"
island["anchor_world"] = list(ANCHOR)
island["top_y_world"] = REQUESTED_TOP_WORLD
island["overhang_m"] = [0.58, 0.42, 0.26, 0.10]
island["waterfall_groove_back_world_x"] = 5.72
island["waterfall_groove_world_z_range"] = [5.04, 6.25]
island["waterfall_clear_world_z_range"] = [5.04, 5.50]
island["waterfall_groove_world_y_range"] = [-11.72, 0.53]
island["east_route_world"] = list(EAST_ROUTE_WORLD)
island["contains_scripts"] = False
island["contains_colliders"] = False

bpy.context.view_layer.update()
depsgraph = bpy.context.evaluated_depsgraph_get()
evaluated = island.evaluated_get(depsgraph)
eval_mesh = evaluated.to_mesh()
eval_mesh.calc_loop_triangles()
bm = bmesh.new()
bm.from_mesh(eval_mesh)
non_manifold = sum(not edge.is_manifold for edge in bm.edges)
zero_area = sum(face.calc_area() < 1e-12 for face in bm.faces)
signed_volume = bm.calc_volume(signed=True)
bm.free()
unity_vertices = [U(v.co) for v in eval_mesh.vertices]
minimum = [min(v[i] for v in unity_vertices) for i in range(3)]
maximum = [max(v[i] for v in unity_vertices) for i in range(3)]
triangles = len(eval_mesh.loop_triangles)
material_slot_count = len(island.data.materials)
material_triangle_counts = {material.name: 0 for material in island.data.materials}
for triangle in eval_mesh.loop_triangles:
    material_triangle_counts[island.data.materials[triangle.material_index].name] += 1
assert triangles <= 15000, triangles
assert material_slot_count <= 4, material_slot_count
assert non_manifold == 0, non_manifold
assert zero_area == 0, zero_area
assert abs(maximum[1]) < 1e-6, maximum[1]
assert -12.0 <= minimum[1] <= -8.0, minimum[1]
evaluated.to_mesh_clear()

# Dense top-plane samples against the authored crown rectangle.
top_errors = []
top_misses = []
for ix in range(25):
    x = -FOUNDATION_HALF_X + 0.0001 + (2 * FOUNDATION_HALF_X - 0.0002) * ix / 24
    for iz in range(21):
        z = -FOUNDATION_HALF_Z + 0.0001 + (2 * FOUNDATION_HALF_Z - 0.0002) * iz / 20
        ray = scene.ray_cast(
            depsgraph, B((x, 2.0, z)), B((0.0, -1.0, 0.0)) - B((0.0, 0.0, 0.0)), distance=4.0)
        hit, location = ray[0], ray[1]
        if not hit:
            top_misses.append([x, z])
        else:
            top_errors.append(U(location).y)
assert not top_misses, top_misses[:4]
assert max(abs(v) for v in top_errors) <= 0.02

# The groove ray starts outside +X and must meet the x=5.72 back plane.
channel_ray = scene.ray_cast(
    depsgraph, B((8.0, -1.0, channel_local_z)), B((-1.0, 0.0, 0.0)) - B((0.0, 0.0, 0.0)), distance=4.0)
channel_hit, channel_location = channel_ray[0], channel_ray[1]
channel_back_x = U(channel_location).x if channel_hit else None
assert channel_hit and abs(channel_back_x - 5.72) <= 0.02, channel_back_x
water_corridor_misses = []
water_corridor_samples = 0
for sample_x in (5.74, 5.90, 6.10):
    for sample_z in (5.10, 5.27, 5.44):
        water_corridor_samples += 1
        hit = scene.ray_cast(
            depsgraph, B((sample_x, 0.74, sample_z)),
            B((0.0, -1.0, 0.0)) - B((0.0, 0.0, 0.0)), distance=12.24)[0]
        if hit:
            water_corridor_misses.append([sample_x, sample_z])
assert not water_corridor_misses, water_corridor_misses

bvh = BVHTree.FromObject(island, depsgraph)
route_local = EAST_ROUTE_WORLD - ANCHOR
nearest, nearest_normal, nearest_index, route_distance = bvh.find_nearest(B(route_local))
assert route_distance is not None and route_distance > 1.0, route_distance

report = {
    "task": TASK,
    "blender_version": bpy.app.version_string,
    "passed": True,
    "units": "metres",
    "authoring_axes": "Unity local +Y up; pivot at top plane centre",
    "anchor_world": list(ANCHOR),
    "requested_top_world_y": REQUESTED_TOP_WORLD,
    "authoring_foundation_top_world_y": FOUNDATION_AUTHORING_TOP_WORLD,
    "authoring_top_alignment_error_m": abs(REQUESTED_TOP_WORLD - FOUNDATION_AUTHORING_TOP_WORLD),
    "foundation_plan_from_authoring_m": [12.36, 9.83],
    "layout_json_note": "20 collision boxes and 7 anchors; no foundation box in JSON",
    "bounds_local_unity": {"min": minimum, "max": maximum,
                           "dimensions": [maximum[i] - minimum[i] for i in range(3)]},
    "overhang_each_side_m": 0.58,
    "masonry_courses": [{"top_local_y": row[1], "height": row[2], "overhang": row[3]} for row in layer_specs],
    "pendant_count": 3,
    "windows": window_records,
    "balcony": {"face": "+X", "center_local_z": balcony_z,
                "maximum_local_x": wall_x + 0.87},
    "waterfall_groove": {"outlet_world": list(WATERFALL_WORLD),
                         "back_world_x": channel_back_x,
                         "world_z_range": [5.04, 6.25],
                         "recommended_water_core_world_z_range": [5.04, 5.50],
                         "world_y_range": [-11.72, 0.53],
                         "outlet_vertical_clearance_m": 0.53 - WATERFALL_WORLD.y,
                         "outside_original_foundation": True,
                         "note": "Task G may keep a wider outlet above the island, but should neck down to a 0.46 m water core inside world z 5.04..5.50 before reaching the island top at y=-0.22.",
                         "vertical_clear_samples": water_corridor_samples,
                         "vertical_clear_misses": len(water_corridor_misses)},
    "east_route": {"world_point": list(EAST_ROUTE_WORLD),
                   "minimum_surface_distance_m": route_distance},
    "topology": {"vertices_evaluated": len(unity_vertices), "triangles": triangles,
                 "material_slots": material_slot_count,
                 "non_manifold_edges": non_manifold, "zero_area_faces": zero_area,
                 "signed_volume_m3": signed_volume,
                 "uv_layers": len(island.data.uv_layers)},
    "top_plane_samples": {"count": len(top_errors), "misses": len(top_misses),
                          "max_abs_error_m": max(abs(v) for v in top_errors)},
    "materials_srgb": {m.name: list(m["palette_srgb"]) for m in PALETTE},
    "triangles_per_material": material_triangle_counts,
    "external_textures": [],
}
(DELIVERY / "Documentation" / "A_FloatingIsland_Blender_Validation.json").write_text(
    json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

# Save a clean, editable one-mesh source with non-destructive triangulation.
island.location = (0.0, 0.0, 0.0)
island.rotation_euler = (0.0, 0.0, 0.0)
island.scale = (1.0, 1.0, 1.0)
source_path = SOURCE / "A_FloatingIsland.blend"
fbx_path = DELIVERY / "FBX" / "A_FloatingIsland.fbx"

# Source-only inspection lighting/camera; these are never exported.
world = bpy.data.worlds.new("A_FloatingIsland_Preview_World")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.43, 0.48, 0.62, 1.0)
world.node_tree.nodes["Background"].inputs[1].default_value = 0.55
scene.world = world


def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


camera_data = bpy.data.cameras.new("Source inspection camera")
camera = bpy.data.objects.new("Source inspection camera", camera_data)
preview_collection.objects.link(camera)
camera.location = B((14.0, 7.0, -18.0))
point_at(camera, B((0.0, -4.3, 0.0)))
camera_data.type = "ORTHO"
camera_data.ortho_scale = 18.5
scene.camera = camera
for name, location, energy, size, color in [
    ("Warm soft key", (-7.0, 12.0, -10.0), 1500.0, 8.0, (1.0, 0.82, 0.70)),
    ("Cool soft fill", (9.0, 3.0, 8.0), 850.0, 9.0, (0.68, 0.77, 1.0)),
]:
    light_data = bpy.data.lights.new(name, "AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = size
    light_data.color = color
    light = bpy.data.objects.new(name, light_data)
    preview_collection.objects.link(light)
    light.location = B(location)
    point_at(light, B((0.0, -3.0, 0.0)))
scene.render.engine = "BLENDER_EEVEE_NEXT"
scene.render.resolution_x = 1400
scene.render.resolution_y = 1100
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = str(DELIVERY / "Preview" / "A_FloatingIsland_Blender.png")
scene.view_settings.view_transform = "Standard"
scene.render.film_transparent = False
activate(island)
bpy.ops.wm.save_as_mainfile(filepath=str(source_path), compress=True)

# Unity's FBX importer mirrors X for this Blender -Z/Y export. Pre-mirror a
# temporary export copy so the imported Unity mesh matches authoring +X exactly.
# The saved .blend and the optional source render remain unmirrored.
export_obj = island.copy()
export_obj.data = island.data.copy()
export_obj.name = "A_FloatingIsland_UnityExport"
asset_collection.objects.link(export_obj)
export_obj.data.transform(Matrix.Diagonal((-1.0, 1.0, 1.0, 1.0)))
recalc(export_obj.data)
activate(export_obj)
export_normals = export_obj.modifiers.new("Unity export area normals", "WEIGHTED_NORMAL")
export_normals.keep_sharp = True
export_normals.weight = 50
apply_modifier(export_obj, export_normals)
bpy.ops.export_scene.fbx(filepath=str(fbx_path), use_selection=True,
                         object_types={"MESH"}, use_mesh_modifiers=True,
                         axis_forward="-Z", axis_up="Y",
                         mesh_smooth_type="FACE", use_custom_props=True,
                         path_mode="AUTO", apply_scale_options="FBX_SCALE_ALL",
                         add_leaf_bones=False, bake_anim=False)
bpy.data.objects.remove(export_obj, do_unlink=True)
print("A_FLOATING_ISLAND_BLEND=" + str(source_path), flush=True)
print("A_FLOATING_ISLAND_FBX=" + str(fbx_path), flush=True)
print("A_FLOATING_ISLAND_TRIANGLES=" + str(triangles), flush=True)
print("A_FLOATING_ISLAND_TOP_ERROR=" + str(max(abs(v) for v in top_errors)), flush=True)
print("A_FLOATING_ISLAND_CHANNEL_X=" + str(channel_back_x), flush=True)
print("A_FLOATING_ISLAND_ROUTE_DISTANCE=" + str(route_distance), flush=True)
if not NO_RENDER:
    bpy.ops.render.render(write_still=True)
print("A_FLOATING_ISLAND_BUILD_COMPLETE", flush=True)
