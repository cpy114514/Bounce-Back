"""Build C-AqueductWalk for Bounce Back, using collision corners as truth.

Run with Blender 5.3:
  blender --background --factory-startup --python-exit-code 1 --python ArtSource/Prototypes/C-AqueductWalk/build_c_aqueduct_walk.py

All coordinates below are Unity world metres until the anchor is removed and the
basis is changed to Blender Z-up. The FBX root is at the anchor's local origin.
Only four static mesh parts are exported; cameras and lights are not exported.
"""
from pathlib import Path
import json
import math
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

SOURCE_DIR = Path(__file__).resolve().parent
PROJECT_DIR = SOURCE_DIR.parents[2]
LAYOUT_PATH = PROJECT_DIR / "ArtSource" / "Santorini Layout" / "Santorini Collision Layout.json"
DELIVERY_DIR = PROJECT_DIR / "Assets" / "Scenes" / "Archive" / "BounceBack" / "Art" / "C-AqueductWalk"
for folder in (DELIVERY_DIR / "FBX", DELIVERY_DIR / "Preview", DELIVERY_DIR / "Documentation"):
    folder.mkdir(parents=True, exist_ok=True)
LOG = (SOURCE_DIR / "blender-build.log").open("w", encoding="utf-8")
sys.stdout = LOG
sys.stderr = LOG

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.name = "C-AqueductWalk"
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.unit_settings.length_unit = "METERS"
asset_collection = bpy.data.collections.new("01_AQUEDUCT_STATIC_MESHES")
scene.collection.children.link(asset_collection)
preview_collection = bpy.data.collections.new("90_CAMERA_LIGHT_ONLY")
scene.collection.children.link(preview_collection)

ANCHOR = Vector((-3.20, 4.27, -2.10))
GROUND_Y = -0.22
STAIR_GAP = 0.05
# Blender FBX's Unity import reflects local X while exchanging Y/Z.  Reflect
# X here as well so Unity recovers (x,y,z), not (-x,y,z), at a zero-rotation root.
C = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
PALETTE = {
    "MV_Ivory": (0.95, 0.88, 0.77),
    "MV_IvoryShade": (0.86, 0.76, 0.70),
    "MV_Lilac": (0.50, 0.45, 0.66),
    "MV_Apricot": (0.90, 0.58, 0.37),
}
IVORY, SHADE, LILAC, APRICOT = range(4)


def linear_channel(value):
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


def make_material(name, srgb):
    linear = tuple(linear_channel(value) for value in srgb)
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = (*linear, 1)
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*linear, 1)
    shader.inputs["Roughness"].default_value = 0.78
    shader.inputs["Metallic"].default_value = 0
    material["sRGB"] = list(srgb)
    return material


MATERIALS = [make_material(name, color) for name, color in PALETTE.items()]
layout = json.loads(LAYOUT_PATH.read_text(encoding="utf-8"))
items = {item["name"]: item for item in layout["items"]}
DECK_NAMES = ["Straight Stair Ramp"] + [f"Turn Ramp {i}" for i in range(1, 5)] + ["Pool Drop Ramp"]
PARAPET_NAMES = [f"Turn Parapet {i} {side}" for i in range(1, 5) for side in (-1, 1)]


def point(item, index):
    value = item["corners"][index]
    return Vector((value["x"], value["y"], value["z"]))


def local_point(world):
    return Vector(world) - ANCHOR


def blender_point(local_unity):
    return C @ Vector((*local_unity, 1))


def unity_from_blender(point):
    value = C.inverted() @ Vector((*point, 1))
    return value.to_3d()


root = bpy.data.objects.new("C-AqueductWalk", None)
asset_collection.objects.link(root)
root.empty_display_type = "PLAIN_AXES"
root.empty_display_size = 0.25
root["anchor_unity"] = list(ANCHOR)
root["pivot"] = "Curve centre at ramp top; place root at anchor with zero rotation"
root["units"] = "metres; Unity +Y up, +Z forward"
root["visual_only"] = True
root["collision_source"] = str(LAYOUT_PATH.relative_to(PROJECT_DIR))


class MeshBuilder:
    def __init__(self):
        self.vertices = []
        self.faces = []
        self.material_indices = []
        self.smooth = []

    def add(self, vertices, faces, material, smooth=False):
        offset = len(self.vertices)
        self.vertices.extend(blender_point(local_point(vertex)).to_3d() for vertex in vertices)
        self.faces.extend(tuple(index + offset for index in face) for face in faces)
        if isinstance(material, int):
            self.material_indices.extend([material] * len(faces))
        else:
            self.material_indices.extend(material)
        self.smooth.extend([smooth] * len(faces))

    def object(self, name):
        mesh = bpy.data.meshes.new(name + "_Mesh")
        mesh.from_pydata(self.vertices, [], self.faces)
        mesh.update()
        for material in MATERIALS:
            mesh.materials.append(material)
        for polygon, material_index, smooth in zip(mesh.polygons, self.material_indices, self.smooth):
            polygon.material_index = material_index
            polygon.use_smooth = smooth
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new(name, mesh)
        asset_collection.objects.link(obj)
        obj.parent = root
        return obj


BOX_FACES = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]


def hexa_vertices(corners, lower_delta=0.0, upper_delta=0.0):
    # JSON box order: bottom 0,1,4,5 and corresponding top 2,3,6,7.
    bottom = [corners[i] + Vector((0, lower_delta, 0)) for i in (0, 1, 5, 4)]
    top = [corners[i] + Vector((0, upper_delta, 0)) for i in (2, 3, 7, 6)]
    return bottom + top


def add_hexa(builder, corners, lower_delta, upper_delta, materials):
    builder.add(hexa_vertices(corners, lower_delta, upper_delta), BOX_FACES, materials)


def add_box(builder, center, tangent, length, height, depth, materials):
    tangent = Vector((tangent[0], 0, tangent[2])).normalized()
    side = Vector((-tangent.z, 0, tangent.x))
    center = Vector(center)
    vertices = []
    for y in (-height * 0.5, height * 0.5):
        for u, v in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            vertices.append(center + tangent * (u * length * 0.5) + side * (v * depth * 0.5) + Vector((0, y, 0)))
    builder.add(vertices, BOX_FACES, materials)


def add_cylinder(builder, center, radius, height, segments=12, material=IVORY):
    center = Vector(center)
    vertices = []
    for y in (0, height):
        for index in range(segments):
            angle = 2 * math.pi * index / segments
            vertices.append(center + Vector((radius * math.cos(angle), y, radius * math.sin(angle))))
    faces = [tuple(reversed(range(segments))), tuple(range(segments, segments * 2))]
    for index in range(segments):
        nxt = (index + 1) % segments
        faces.append((index, nxt, segments + nxt, segments + index))
    builder.add(vertices, faces, material, smooth=True)


def add_dome(builder, base, radius=0.105, rise=0.095, segments=12, rings=4):
    base = Vector(base)
    vertices = []
    for ring in range(rings):
        angle = (math.pi * 0.5) * ring / rings
        ring_radius = radius * math.cos(angle)
        y = rise * math.sin(angle)
        for index in range(segments):
            theta = 2 * math.pi * index / segments
            vertices.append(base + Vector((ring_radius * math.cos(theta), y, ring_radius * math.sin(theta))))
    apex = len(vertices)
    vertices.append(base + Vector((0, rise, 0)))
    faces = [tuple(reversed(range(segments)))]
    for ring in range(rings - 1):
        for index in range(segments):
            nxt = (index + 1) % segments
            a = ring * segments + index
            b = ring * segments + nxt
            c = (ring + 1) * segments + nxt
            d = (ring + 1) * segments + index
            faces.append((a, b, c, d))
    last_ring = (rings - 1) * segments
    for index in range(segments):
        nxt = (index + 1) % segments
        faces.append((last_ring + index, last_ring + nxt, apex))
    builder.add(vertices, faces, APRICOT, smooth=True)


def add_uv(obj):
    mesh = obj.data
    if not mesh.uv_layers:
        mesh.uv_layers.new(name="UV0_Metric")
    uv = mesh.uv_layers.active
    for polygon in mesh.polygons:
        dominant = max(range(3), key=lambda axis: abs(polygon.normal[axis]))
        axes = ((1, 2), (0, 2), (0, 1))[dominant]
        for loop_index in polygon.loop_indices:
            co = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            uv.data[loop_index].uv = (co[axes[0]], co[axes[1]])


def union_objects(objects, name):
    base = objects[0]
    for other in objects[1:]:
        bpy.context.view_layer.objects.active = base
        modifier = base.modifiers.new("Exact collision footprint union", "BOOLEAN")
        modifier.operation = "UNION"
        modifier.solver = "EXACT"
        modifier.object = other
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        bpy.data.objects.remove(other, do_unlink=True)
    base.name = name
    return base


def deck_materials(obj, upper):
    for polygon in obj.data.polygons:
        if polygon.normal.z > 0.65:
            polygon.material_index = IVORY if upper else SHADE
        elif polygon.normal.z < -0.65:
            polygon.material_index = SHADE
        else:
            polygon.material_index = APRICOT if upper else IVORY
        polygon.use_smooth = False


# The deck is the exact vertical union of the six collision prisms. A separate
# 9 cm upper band gives the apricot cornice without moving any collision face.
deck_parts = []
for upper in (False, True):
    pieces = []
    for name in DECK_NAMES:
        corners = [point(items[name], index) for index in range(8)]
        builder = MeshBuilder()
        band_corners = list(corners)
        for bottom_index, top_index in ((0, 2), (1, 3), (4, 6), (5, 7)):
            bottom = corners[bottom_index]
            top = corners[top_index]
            drop = 0.091 if upper else 0.09
            fraction = drop / (top.y - bottom.y)
            # Follow the actual rotated collider edge. A vertical-only offset
            # would leave the sloped ramp's side plane by several centimetres.
            band_corners[bottom_index if upper else top_index] = top.lerp(bottom, fraction)
        add_hexa(builder, band_corners, 0.0, 0.0, IVORY)
        pieces.append(builder.object(("Upper_" if upper else "Lower_") + name))
    deck = union_objects(pieces, "Deck_Cornice" if upper else "Deck_Structure")
    deck_materials(deck, upper)
    deck_parts.append(deck)
deck = union_objects(deck_parts, "C-AqueductWalk_Deck")
# Remove numerical slivers from the coplanar cornice/structure union while
# preserving the collision-derived outer surface and its material boundaries.
bm = bmesh.new()
bm.from_mesh(deck.data)
bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-5)
bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-5)
bmesh.ops.triangulate(bm, faces=list(bm.faces), quad_method="BEAUTY", ngon_method="BEAUTY")
bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-5)
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
bm.to_mesh(deck.data)
bm.free()
deck.data.update()
deck["role"] = "Exact union of six collision decks; 9 cm apricot side cornice"

# Parapets use every collision corner exactly; caps and dome posts stay on top.
parapet_builder = MeshBuilder()
for name in PARAPET_NAMES:
    corners = [point(items[name], index) for index in range(8)]
    add_hexa(parapet_builder, corners, 0, -0.07, [SHADE, SHADE, IVORY, IVORY, SHADE, IVORY])
    cap_corners = list(corners)
    for bottom_index, top_index in ((0, 2), (1, 3), (4, 6), (5, 7)):
        cap_corners[bottom_index] = corners[top_index] + Vector((0, -0.07, 0))
    add_hexa(parapet_builder, cap_corners, 0, 0, [SHADE, IVORY, APRICOT, APRICOT, APRICOT, APRICOT])
wall_vertex_count = len(parapet_builder.vertices)

outer_post_positions = []
for index in range(1, 5):
    item = items[f"Turn Parapet {index} 1"]
    top = [point(item, corner) for corner in (2, 3, 7, 6)]
    edges = [(top[i], top[(i + 1) % 4]) for i in range(4)]
    a, b = max(edges, key=lambda pair: (pair[1] - pair[0]).length_squared)
    radial = Vector(((a.x + b.x) * 0.5 - ANCHOR.x, 0, (a.z + b.z) * 0.5 - ANCHOR.z)).normalized()
    tangent = Vector((-radial.z, 0, radial.x))
    if (b - a).dot(tangent) < 0:
        a, b = b, a
    if not outer_post_positions:
        outer_post_positions.append(a)
    outer_post_positions.append((a + b) * 0.5)
    if index == 4:
        outer_post_positions.append(b)
for position in outer_post_positions:
    add_cylinder(parapet_builder, position, 0.055, 0.12, 12, IVORY)
    add_dome(parapet_builder, position + Vector((0, 0.12, 0)))
parapet = parapet_builder.object("C-AqueductWalk_Parapet")
parapet["role"] = "Eight exact collision walls with six small dome posts"


def add_arch(builder, p0, under0, p1, under1, depth=0.28, pier_width=0.28, segments=16):
    p0, p1 = Vector(p0), Vector(p1)
    delta = p1 - p0
    length = math.hypot(delta.x, delta.z)
    tangent = Vector((delta.x, 0, delta.z)).normalized()
    side = Vector((-tangent.z, 0, tangent.x))
    clear_radius = (length - pier_width) * 0.5
    assert clear_radius > 0.20, (p0, p1, length)
    spring = min(under0, under1) - clear_radius - 0.22

    def position(u, y, v):
        return p0 + tangent * u + side * v + Vector((0, y, 0))

    vertices = []
    for v in (-depth * 0.5, depth * 0.5):
        for is_top in (False, True):
            for index in range(segments + 1):
                angle = math.pi * index / segments
                u = length * 0.5 - clear_radius * math.cos(angle)
                arch_y = spring + clear_radius * math.sin(angle)
                top_y = under0 + (under1 - under0) * (u / length)
                vertices.append(position(u, top_y if is_top else arch_y, v))
    row = segments + 1
    front_arch, front_top, back_arch, back_top = 0, row, row * 2, row * 3
    offset = len(builder.vertices)
    builder.vertices.extend(blender_point(local_point(vertex)).to_3d() for vertex in vertices)
    for index in range(segments):
        nxt = index + 1
        faces = [
            (front_arch + index, front_arch + nxt, front_top + nxt, front_top + index),
            (back_arch + nxt, back_arch + index, back_top + index, back_top + nxt),
            (front_arch + nxt, back_arch + nxt, back_arch + index, front_arch + index),
            (front_top + index, front_top + nxt, back_top + nxt, back_top + index),
        ]
        builder.faces.extend(tuple(vertex + offset for vertex in face) for face in faces)
        builder.material_indices.extend((IVORY, IVORY, LILAC, SHADE))
        builder.smooth.extend((False, False, True, False))
    cap_faces = [
        (front_arch, front_top, back_top, back_arch),
        (front_arch + segments, back_arch + segments, back_top + segments, front_top + segments),
    ]
    builder.faces.extend(tuple(vertex + offset for vertex in face) for face in cap_faces)
    builder.material_indices.extend((SHADE, SHADE))
    builder.smooth.extend((False, False))


def add_pier(builder, position, underside, tangent, inner=False):
    position = Vector(position)
    width = 0.24 if inner else 0.28
    depth = 0.34 if inner else 0.40
    add_box(builder, position + Vector((0, GROUND_Y + 0.08, 0)), tangent, width + 0.18, 0.16, depth + 0.16,
            [SHADE, SHADE, SHADE, SHADE, SHADE, SHADE])
    shaft_bottom = GROUND_Y + 0.16
    shaft_top = underside - 0.12
    add_box(builder, position + Vector((0, (shaft_bottom + shaft_top) * 0.5, 0)), tangent,
            width, shaft_top - shaft_bottom, depth, [IVORY, IVORY, IVORY, SHADE, IVORY, SHADE])
    add_box(builder, position + Vector((0, underside - 0.06, 0)), tangent, width + 0.16, 0.12, depth + 0.12,
            [SHADE, APRICOT, APRICOT, APRICOT, APRICOT, APRICOT])


# Six bays: one straight, three curved, two on the pool drop. The last support
# stops at world Z=-0.25, ahead of the launch lip at Z=0.019205.
outer = [Vector((-4.539662, 0, -4.235)), Vector((-3.20, 0, -4.30))]
for angle_deg in (-60, -30, 0):
    angle = math.radians(angle_deg)
    outer.append(Vector((ANCHOR.x + 2.20 * math.cos(angle), 0, ANCHOR.z + 2.20 * math.sin(angle))))
outer.extend((Vector((-1.0, 0, -1.07)), Vector((-1.0, 0, -0.25))))
inner = [Vector((-4.539662, 0, -3.065)), Vector((-3.20, 0, -3.08))]
for angle_deg in (-60, -30, 0):
    angle = math.radians(angle_deg)
    inner.append(Vector((ANCHOR.x + 0.98 * math.cos(angle), 0, ANCHOR.z + 0.98 * math.sin(angle))))
inner.extend((Vector((-2.26, 0, -1.07)), Vector((-2.26, 0, -0.25))))
undersides = [4.4451866 - 0.35, 3.92, 3.92, 3.92, 3.92, 3.062, 2.474]
arcade_builder = MeshBuilder()
for index in range(6):
    tangent = outer[index + 1] - outer[index]
    add_arch(arcade_builder, outer[index], undersides[index], outer[index + 1], undersides[index + 1])
for index in range(7):
    tangent = outer[min(index + 1, 6)] - outer[max(index - 1, 0)]
    add_pier(arcade_builder, outer[index], undersides[index], tangent)
    add_pier(arcade_builder, inner[index], undersides[index], tangent, inner=True)
    beam_delta = outer[index] - inner[index]
    beam_center = (outer[index] + inner[index]) * 0.5
    beam_center.y = undersides[index] - 0.09
    add_box(arcade_builder, beam_center, beam_delta, beam_delta.length + 0.16, 0.18, 0.24,
            [SHADE, SHADE, SHADE, SHADE, SHADE, SHADE])
arcade = arcade_builder.object("C-AqueductWalk_Arcade")
arcade["role"] = "Six through-arch bays; outer arcade plus inner piers and cross beams"
arcade["arch_count"] = 6
arcade["last_support_world_z"] = -0.25

# Decorative donkey stair band, always outside collision and 12 cm below the
# adjacent road at its highest tread. It stops at world Z=-0.28.
stair_builder = MeshBuilder()
straight_start_x = -4.50
turn1_inner_a = point(items["Turn Ramp 1"], 2)
turn1_inner_b = point(items["Turn Ramp 1"], 6)
for index in range(8):
    x0 = straight_start_x + 1.30 * index / 8
    x1 = straight_start_x + 1.30 * (index + 1) / 8
    road = 4.44 + (4.27 - 4.44) * ((x1 - straight_start_x) / 1.30)
    top = road - 0.12
    # Turn Ramp 1 overlaps the straight ramp in plan. Shift the whole 0.50 m
    # band away from its sloping inner edge rather than clipping off a corner.
    outer_z = max(point(items["Straight Stair Ramp"], corner).z for corner in (2, 3, 6, 7)) + STAIR_GAP
    edge_min_x = min(turn1_inner_a.x, turn1_inner_b.x)
    edge_max_x = max(turn1_inner_a.x, turn1_inner_b.x)
    xa, xb = x0 - 0.003, x1 + 0.003
    if xb >= edge_min_x and xa <= edge_max_x:
        for x in (max(xa, edge_min_x), min(xb, edge_max_x)):
            fraction = (x - turn1_inner_a.x) / (turn1_inner_b.x - turn1_inner_a.x)
            edge_z = turn1_inner_a.z + fraction * (turn1_inner_b.z - turn1_inner_a.z)
            outer_z = max(outer_z, edge_z + STAIR_GAP)
    stair_z = outer_z + 0.25
    add_box(stair_builder, Vector(((x0 + x1) * 0.5, top - 0.07, stair_z)), (1, 0, 0), x1 - x0 + 0.006, 0.14, 0.50,
            [SHADE, IVORY, APRICOT, SHADE, IVORY, IVORY])
    add_box(stair_builder, Vector(((x0 + x1) * 0.5, top - 0.21, stair_z)), (1, 0, 0), x1 - x0 + 0.006, 0.14, 0.50,
            SHADE)

turn_polygons = []
for index in range(1, 5):
    item = items[f"Turn Ramp {index}"]
    turn_polygons.append([point(item, corner) for corner in (2, 3, 7, 6)])


def cross2(a, b):
    return a.x * b.y - a.y * b.x


def inner_collision_radius(angle):
    direction = Vector((math.cos(angle), math.sin(angle)))
    origin = Vector((ANCHOR.x, ANCHOR.z))
    distances = []
    for polygon in turn_polygons:
        for index, point_a in enumerate(polygon):
            point_b = polygon[(index + 1) % len(polygon)]
            a = Vector((point_a.x, point_a.z))
            edge = Vector((point_b.x, point_b.z)) - a
            denominator = cross2(direction, edge)
            if abs(denominator) < 1e-8:
                continue
            relative = a - origin
            distance = cross2(relative, edge) / denominator
            fraction = cross2(relative, direction) / denominator
            if distance > 1e-6 and -1e-6 <= fraction <= 1.000001:
                distances.append(distance)
    assert distances, math.degrees(angle)
    return min(distances)


for index in range(18):
    a0 = math.radians(-90 + index * 5)
    a1 = math.radians(-90 + (index + 1) * 5)
    outer0 = inner_collision_radius(a0) - STAIR_GAP
    outer1 = inner_collision_radius(a1) - STAIR_GAP
    inner0, inner1 = outer0 - 0.50, outer1 - 0.50
    assert inner0 > 0.05 and inner1 > 0.05
    for bottom, top, material in ((4.01, 4.15, IVORY), (3.87, 4.01, SHADE)):
        vertices = []
        for y in (bottom, top):
            for radius, angle in ((inner0, a0), (outer0, a0), (outer1, a1), (inner1, a1)):
                vertices.append(Vector((ANCHOR.x + radius * math.cos(angle), y, ANCHOR.z + radius * math.sin(angle))))
        stair_builder.add(vertices, BOX_FACES, [SHADE, material, SHADE, APRICOT, SHADE, SHADE])

pool_start_z = -2.119048953
pool_end_z = 0.019204855
pool_start_y = 4.2860312
pool_end_y = 2.5190487
pool_inner_x = min(point(items["Pool Drop Ramp"], corner).x for corner in (2, 3, 6, 7))
pool_stair_x = pool_inner_x - STAIR_GAP - 0.25
for index in range(8):
    z0 = -2.10 + 1.82 * index / 8
    z1 = -2.10 + 1.82 * (index + 1) / 8
    road = pool_start_y + (pool_end_y - pool_start_y) * ((z1 - pool_start_z) / (pool_end_z - pool_start_z))
    top = road - 0.12
    add_box(stair_builder, Vector((pool_stair_x, top - 0.07, (z0 + z1) * 0.5)), (0, 0, 1), z1 - z0 + 0.006, 0.14, 0.50,
            [SHADE, IVORY, SHADE, APRICOT, IVORY, IVORY])
    add_box(stair_builder, Vector((pool_stair_x, top - 0.21, (z0 + z1) * 0.5)), (0, 0, 1), z1 - z0 + 0.006, 0.14, 0.50,
            SHADE)
stairs = stair_builder.object("C-AqueductWalk_Donkey_Stairs")
stairs["role"] = "0.50 m decorative stair band outside collision; treads below road"
stairs["band_width_m"] = 0.50
stairs["collision_gap_m"] = STAIR_GAP
stairs["last_step_world_z"] = -0.28

mesh_objects = [deck, parapet, arcade, stairs]
for obj in mesh_objects:
    add_uv(obj)
    obj["visual_only"] = True

bpy.context.view_layer.update()
depsgraph = bpy.context.evaluated_depsgraph_get()


def topology(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    result = {
        "vertices": len(bm.verts),
        "triangles": sum(len(face.verts) - 2 for face in bm.faces),
        "non_manifold_edges": sum(not edge.is_manifold for edge in bm.edges),
        "zero_area_faces": sum(face.calc_area() < 1e-10 for face in bm.faces),
    }
    bm.free()
    return result


def max_corner_error(obj, names, indices):
    tree = BVHTree.FromObject(obj, depsgraph)
    maximum = 0.0
    for name in names:
        for index in indices:
            target = blender_point(local_point(point(items[name], index))).to_3d()
            nearest = tree.find_nearest(target)
            maximum = max(maximum, nearest[3])
    return maximum


reports = {obj.name: topology(obj) for obj in mesh_objects}
for name, report in reports.items():
    assert report["non_manifold_edges"] == 0, (name, report)
    assert report["zero_area_faces"] == 0, (name, report)
total_triangles = sum(report["triangles"] for report in reports.values())
assert total_triangles <= 15000, total_triangles
max_top_error = max_corner_error(deck, DECK_NAMES, (2, 3, 6, 7))
max_parapet_error = max_corner_error(parapet, PARAPET_NAMES, range(8))
# Every external deck vertex must lie on an original collision face. The union
# buries overlapping collision interiors; those hidden bottom corners are not
# external art surfaces and are deliberately not treated as visible deviations.
surface_vertices = []
surface_faces = []
for name in DECK_NAMES:
    corners = [point(items[name], index) for index in range(8)]
    offset = len(surface_vertices)
    surface_vertices.extend(blender_point(local_point(vertex)).to_3d() for vertex in hexa_vertices(corners))
    surface_faces.extend(tuple(offset + index for index in face) for face in BOX_FACES)
collision_surface = BVHTree.FromPolygons(surface_vertices, surface_faces, all_triangles=False)
parapet_surface_vertices = []
parapet_surface_faces = []
for name in PARAPET_NAMES:
    corners = [point(items[name], index) for index in range(8)]
    offset = len(parapet_surface_vertices)
    parapet_surface_vertices.extend(blender_point(local_point(vertex)).to_3d() for vertex in hexa_vertices(corners))
    parapet_surface_faces.extend(tuple(offset + index for index in face) for face in BOX_FACES)
parapet_collision_surface = BVHTree.FromPolygons(parapet_surface_vertices, parapet_surface_faces, all_triangles=False)
max_external_parapet_wall_error = max(parapet_collision_surface.find_nearest(parapet.data.vertices[index].co)[3] for index in range(wall_vertex_count))
external_errors = [collision_surface.find_nearest(vertex.co)[3] for vertex in deck.data.vertices]
max_external_deck_error = max(external_errors)
worst_vertex = max(range(len(external_errors)), key=external_errors.__getitem__)
worst_world = ANCHOR + unity_from_blender(deck.data.vertices[worst_vertex].co)
print("WORST_EXTERNAL_DECK_VERTEX", worst_vertex, tuple(worst_world), max_external_deck_error,
      [(tuple(polygon.normal), polygon.material_index, polygon.area) for polygon in deck.data.polygons if worst_vertex in polygon.vertices])
assert max_top_error <= 0.02, max_top_error
assert max_external_deck_error <= 0.02, max_external_deck_error
assert max_parapet_error <= 0.02, max_parapet_error
assert max_external_parapet_wall_error <= 0.02, max_external_parapet_wall_error

all_local = [unity_from_blender(vertex.co) for obj in mesh_objects for vertex in obj.data.vertices]
all_world = [point + ANCHOR for point in all_local]
minimum = [min(point[axis] for point in all_world) for axis in range(3)]
maximum = [max(point[axis] for point in all_world) for axis in range(3)]
assert abs(minimum[1] - GROUND_Y) < 1e-5
assert maximum[2] <= 0.025, maximum[2]

# Export only the root and its four static mesh children.
bpy.ops.object.select_all(action="DESELECT")
root.select_set(True)
for obj in mesh_objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = deck
fbx_path = DELIVERY_DIR / "FBX" / "C-AqueductWalk.fbx"
kwargs = dict(filepath=str(fbx_path), use_selection=True, object_types={"EMPTY", "MESH"},
              global_scale=1.0, apply_unit_scale=True, axis_forward="-Z", axis_up="Y",
              use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
              path_mode="AUTO", bake_anim=False)
supported = bpy.ops.export_scene.fbx.get_rna_type().properties.keys()
bpy.ops.export_scene.fbx(**{key: value for key, value in kwargs.items() if key in supported})

# Source-file inspection camera and lights only; no extra mesh is added.
def camera_object(name, position, target, ortho_scale):
    data = bpy.data.cameras.new(name)
    data.type = "ORTHO"
    data.ortho_scale = ortho_scale
    obj = bpy.data.objects.new(name, data)
    preview_collection.objects.link(obj)
    obj.location = blender_point(local_point(Vector(position))).to_3d()
    direction = (blender_point(local_point(Vector(target))).to_3d() - obj.location).normalized()
    right = direction.cross(Vector((0, 0, 1))).normalized()
    up = right.cross(direction).normalized()
    backward = -direction
    obj.rotation_euler = Matrix(((right.x, up.x, backward.x), (right.y, up.y, backward.y), (right.z, up.z, backward.z))).to_euler()
    return obj


focus = Vector((-0.05, 0.95, 0.25))
formal = camera_object("Formal_Camera", focus + Vector((12, 15, -15)), focus, 19.0 * 16 / 9)
reverse = camera_object("Reverse_Camera", (5, 1.5, 5.2), (-4.5, 2.35, -3.0), 13.5)
scene.camera = formal
world = bpy.data.worlds.new("Preview_Sky")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.68, 0.75, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.65
scene.world = world
sun_data = bpy.data.lights.new("Preview_Sun", "SUN")
sun_data.energy = 1.8
sun_data.angle = math.radians(12)
sun = bpy.data.objects.new("Preview_Sun", sun_data)
preview_collection.objects.link(sun)
sun.rotation_euler = (math.radians(28), math.radians(-25), math.radians(-30))
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == "VIEW_3D":
            area.spaces.active.region_3d.view_perspective = "CAMERA"
            area.spaces.active.shading.type = "MATERIAL"
bpy.ops.object.select_all(action="DESELECT")
root.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_DIR / "C-AqueductWalk.blend"), check_existing=False)

manifest = {
    "task": "C-AqueductWalk",
    "source_blend": "ArtSource/Prototypes/C-AqueductWalk/C-AqueductWalk.blend",
    "rebuild_script": "ArtSource/Prototypes/C-AqueductWalk/build_c_aqueduct_walk.py",
    "fbx": "Assets/Scenes/Archive/BounceBack/Art/C-AqueductWalk/FBX/C-AqueductWalk.fbx",
    "collision_layout": "ArtSource/Santorini Layout/Santorini Collision Layout.json",
    "anchor_unity_world": list(ANCHOR),
    "pivot": "turn centre at ramp top Y=4.27; root rotation zero",
    "units": "metres; Unity +Y up, +Z forward",
    "bounds_world": {"min": minimum, "max": maximum},
    "dimensions_m": [maximum[i] - minimum[i] for i in range(3)],
    "material_slots": list(PALETTE),
    "material_srgb": PALETTE,
    "mesh_parts": reports,
    "total_triangles": total_triangles,
    "arch_bays": 6,
    "dome_posts": len(outer_post_positions),
    "stair_band": {"width_m": 0.50, "collision_gap_m": STAIR_GAP, "below_road_m": 0.12, "last_step_world_z": -0.28},
    "support": {"ground_y": GROUND_Y, "last_support_world_z": -0.25},
    "alignment": {
        "max_deck_top_corner_error_m": max_top_error,
        "max_external_deck_vertex_error_m": max_external_deck_error,
        "max_parapet_corner_error_m": max_parapet_error,
        "max_external_parapet_wall_vertex_error_m": max_external_parapet_wall_error,
        "tolerance_m": 0.02,
        "note": "External deck vertices are measured against all original collision faces; boolean-union-buried overlap interiors are excluded.",
    },
    "launch_clearance": {"pool_target": [-1.40, 0.45, 2.05], "max_art_world_z": maximum[2], "clear_beyond_z": 0.025},
    "contains_colliders": False,
    "contains_scripts": False,
    "external_textures": [],
}
(DELIVERY_DIR / "Documentation" / "C-AqueductWalk_Geometry.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
print("C_AQUEDUCT_WALK_BUILD_COMPLETE", fbx_path)
print(json.dumps(manifest, indent=2))
LOG.close()
