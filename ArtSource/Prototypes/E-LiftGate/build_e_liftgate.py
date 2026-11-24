"""Build the Santorini lift-gate frame and shutter.

Run:
  blender.exe --background --python build_e_liftgate.py

Meshes are authored in Unity metres (X right, Y up, Z forward). The shutter and
the lintel live in the lift wall's local space: origin at the wall centre,
local +Z is the glazed front. The piers live in a yaw-only space that shares
the wall's horizontal axes but stays vertical, so their bases sit on y = -0.22.

FBX export uses Blender's -Z forward / Y up conversion. Unity prefabs apply the
wall rotation on an internal Orientation node; the prefab root stays at identity.
"""
from pathlib import Path
import json
import math
import sys

import bpy
import bmesh
from mathutils import Matrix, Quaternion, Vector

SOURCE_DIR = Path(__file__).resolve().parent
PROJECT_DIR = SOURCE_DIR.parents[2]
ART_DIR = PROJECT_DIR / "Assets" / "Scenes" / "Archive" / "BounceBack" / "Art" / "E-LiftGate"
FBX_DIR = ART_DIR / "FBX"
QA_DIR = SOURCE_DIR / "qa"
for folder in (FBX_DIR, QA_DIR):
    folder.mkdir(parents=True, exist_ok=True)

LOG_FILE = (SOURCE_DIR / "blender-build.log").open("w", encoding="utf-8")
sys.stdout = LOG_FILE
sys.stderr = LOG_FILE

# Lift Wall, from Santorini Collision Layout.json / SantoriniSystemsAuthoring.
WALL_CENTER = (-1.55, 2.15, 3.95)
FRAME_ANCHOR = (-1.55, -0.22, 3.95)
ORIENTATION_OFFSET = (0.0, 2.37, 0.0)  # wall centre relative to the frame anchor
WALL_QUAT = (0.01241377368569374, 0.9237968921661377, -0.030059847980737687, 0.38149914145469668)
HALF = (1.30, 0.625, 0.145)  # 2.60 x 1.25 x 0.29
PIER_X = 1.56
SEAM = 0.625
MEDALLION_RADIUS = 0.30
MEDALLION_X = (-0.72, 0.0, 0.72)

JSON_CORNERS = (
    (-0.7595740556716919, 1.5357487201690674, 4.998604774475098),
    (-2.601956367492676, 1.5357487201690674, 3.1640405654907228),
    (-0.7022350430488586, 2.783104419708252, 4.94102144241333),
    (-2.5446174144744875, 2.783104419708252, 3.106456995010376),
    (-0.5553824305534363, 1.5168957710266114, 4.793542861938477),
    (-2.3977649211883547, 1.5168957710266114, 2.9589786529541017),
    (-0.498043417930603, 2.764251470565796, 4.735959529876709),
    (-2.340425968170166, 2.764251470565796, 2.901395320892334),
)


def srgb_to_linear(channel):
    if channel <= 0.04045:
        return channel / 12.92
    return ((channel + 0.055) / 1.055) ** 2.4


def linear_color(rgb):
    return tuple(srgb_to_linear(channel) for channel in rgb)


def length(vector):
    return math.sqrt(sum(channel * channel for channel in vector))


def normalize(vector):
    scale = length(vector)
    return tuple(channel / scale for channel in vector)


def dot(left, right):
    return left[0] * right[0] + left[1] * right[1] + left[2] * right[2]


def cross(left, right):
    return (
        left[1] * right[2] - left[2] * right[1],
        left[2] * right[0] - left[0] * right[2],
        left[0] * right[1] - left[1] * right[0],
    )


def add(left, right, scale=1.0):
    return tuple(left[axis] + scale * right[axis] for axis in range(3))


def quat_to_axes(quaternion):
    """Unity quaternion (x, y, z, w) -> right, up, forward columns."""
    x, y, z, w = quaternion
    return (
        (1 - 2 * (y * y + z * z), 2 * (x * y + z * w), 2 * (x * z - y * w)),
        (2 * (x * y - z * w), 1 - 2 * (x * x + z * z), 2 * (y * z + x * w)),
        (2 * (x * z + y * w), 2 * (y * z - x * w), 1 - 2 * (x * x + y * y)),
    )


def matrix_to_quat(right, up, forward):
    m00, m10, m20 = right
    m01, m11, m21 = up
    m02, m12, m22 = forward
    trace = m00 + m11 + m22
    if trace > 0:
        scale = math.sqrt(trace + 1.0) * 2
        w = 0.25 * scale
        x = (m21 - m12) / scale
        y = (m02 - m20) / scale
        z = (m10 - m01) / scale
    elif m00 > m11 and m00 > m22:
        scale = math.sqrt(1 + m00 - m11 - m22) * 2
        w = (m21 - m12) / scale
        x = 0.25 * scale
        y = (m01 + m10) / scale
        z = (m02 + m20) / scale
    elif m11 > m22:
        scale = math.sqrt(1 + m11 - m00 - m22) * 2
        w = (m02 - m20) / scale
        x = (m01 + m10) / scale
        y = 0.25 * scale
        z = (m12 + m21) / scale
    else:
        scale = math.sqrt(1 + m22 - m00 - m11) * 2
        w = (m10 - m01) / scale
        x = (m02 + m20) / scale
        y = (m12 + m21) / scale
        z = 0.25 * scale
    return normalize((x, y, z, w))


def look_rotation(forward, up=(0.0, 1.0, 0.0)):
    """Unity Quaternion.LookRotation."""
    axis_z = normalize(forward)
    axis_x = normalize(cross(up, axis_z))
    axis_y = cross(axis_z, axis_x)
    return matrix_to_quat(axis_x, axis_y, axis_z), (axis_x, axis_y, axis_z)


def transform_point(origin, axes, point):
    return add(add(add(origin, axes[0], point[0]), axes[1], point[1]), axes[2], point[2])


WALL_AXES = quat_to_axes(WALL_QUAT)
HORIZONTAL_FORWARD = normalize((WALL_AXES[2][0], 0.0, WALL_AXES[2][2]))
YAW_QUAT, YAW_AXES = look_rotation(HORIZONTAL_FORWARD)
RIGHT_ERROR = length(add(WALL_AXES[0], YAW_AXES[0], -1))
assert RIGHT_ERROR < 1e-4, RIGHT_ERROR
assert abs(dot(YAW_AXES[1], (0, 1, 0)) - 1) < 1e-6

GENERATED_CORNERS = []
for sign_x in (-1, 1):
    for sign_y in (-1, 1):
        for sign_z in (-1, 1):
            local = (sign_x * HALF[0], sign_y * HALF[1], sign_z * HALF[2])
            GENERATED_CORNERS.append(transform_point(WALL_CENTER, WALL_AXES, local))
for generated in GENERATED_CORNERS:
    nearest = min(length(add(generated, corner, -1)) for corner in JSON_CORNERS)
    assert nearest < 0.002, (generated, nearest)


def blender_point(point):
    return Vector((point[0], -point[2], point[1]))


def blender_quaternion(quaternion):
    rotation = Quaternion((quaternion[3], quaternion[0], quaternion[1], quaternion[2])).to_matrix().to_4x4()
    change = Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    return (change @ rotation @ change.inverted()).to_quaternion()


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.name = "E-LiftGate"
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.unit_settings.length_unit = "METERS"

COLLECTIONS = {}
for name in ("00_SHUTTER", "01_FRAME_LINTEL", "02_FRAME_PIERS", "90_PREVIEW"):
    collection = bpy.data.collections.new(name)
    scene.collection.children.link(collection)
    COLLECTIONS[name] = collection


def make_material(name, srgb, roughness):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    color = (*linear_color(srgb), 1.0)
    material.diffuse_color = color
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Metallic"].default_value = 0.0
    return material


MATERIALS = {
    "MV_Ivory": make_material("MV_Ivory", (0.95, 0.88, 0.77), 0.78),
    "MV_Apricot": make_material("MV_Apricot", (0.90, 0.58, 0.37), 0.62),
    "MV_Mint": make_material("MV_Mint", (0.36, 0.70, 0.55), 0.28),
    "MV_DomeBlue": make_material("MV_DomeBlue", (0.30, 0.47, 0.80), 0.48),
    "Preview_Ground": make_material("Preview_Ground", (0.74, 0.45, 0.47), 0.9),
    "Preview_Sky": make_material("Preview_Sky", (0.83, 0.78, 0.74), 1.0),
}


class Solid:
    def __init__(self, material):
        self.material = material
        self.vertices = []
        self.faces = []

    def _add(self, vertices, faces):
        offset = len(self.vertices)
        self.vertices.extend(vertices)
        self.faces.extend(tuple(index + offset for index in face) for face in faces)

    def box(self, minimum, maximum):
        x0, y0, z0 = minimum
        x1, y1, z1 = maximum
        if x1 < x0 or y1 < y0 or z1 < z0:
            return
        # Overlap of a millimetre is applied by the caller where boxes meet.
        vertices = [
            (x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
            (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1),
        ]
        faces = (
            (0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
            (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7),
        )
        self._add(vertices, faces)

    def extrude_z(self, polygon, z0, z1):
        count = len(polygon)
        vertices = [(x, y, z0) for x, y in polygon] + [(x, y, z1) for x, y in polygon]
        faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
        for index in range(count):
            nxt = (index + 1) % count
            faces.append((index, nxt, nxt + count, index + count))
        self._add(vertices, faces)

    def extrude_y(self, polygon, y0, y1):
        count = len(polygon)
        vertices = [(x, y0, z) for x, z in polygon] + [(x, y1, z) for x, z in polygon]
        faces = [tuple(range(count)), tuple(reversed(range(count, count * 2)))]
        for index in range(count):
            nxt = (index + 1) % count
            faces.append((index, index + count, nxt + count, nxt))
        self._add(vertices, faces)


def half_disk(center_x, center_y, radius, upper, segments=14):
    points = []
    for index in range(segments + 1):
        if upper:
            angle = math.pi * index / segments
        else:
            angle = math.pi + math.pi * index / segments
        points.append((center_x + radius * math.cos(angle), center_y + radius * math.sin(angle)))
    return points


def arc_ring(center, inner_radius, outer_radius, start, end, segments):
    outer = []
    inner = []
    for index in range(segments + 1):
        angle = start + (end - start) * index / segments
        outer.append((center[0] + outer_radius * math.cos(angle), center[1] + outer_radius * math.sin(angle)))
        inner.append((center[0] + inner_radius * math.cos(angle), center[1] + inner_radius * math.sin(angle)))
    return outer + list(reversed(inner))


def commit_object(solid, name, collection, parent):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    vertices = [tuple(blender_point(vertex)) for vertex in solid.vertices]
    mesh.from_pydata(vertices, [], solid.faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bm.normal_update()
    if bm.calc_volume(signed=True) < 0:
        bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
        bm.normal_update()
    for face in bm.faces:
        face.smooth = False
    volume = bm.calc_volume(signed=True)
    non_manifold = sum(not edge.is_manifold for edge in bm.edges)
    triangles = len(bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    mesh.validate()
    mesh.materials.append(MATERIALS[solid.material])
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = parent
    obj["material"] = solid.material
    obj["triangles"] = triangles
    obj["signed_volume_m3"] = volume
    obj["non_manifold_edges"] = non_manifold
    assert non_manifold == 0, (name, non_manifold)
    assert volume > 0, (name, volume)
    assert triangles > 0, name
    return obj


def new_root(name, collection):
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.2
    collection.objects.link(root)
    return root


def bounds_of(vertices):
    minimum = [min(vertex[axis] for vertex in vertices) for axis in range(3)]
    maximum = [max(vertex[axis] for vertex in vertices) for axis in range(3)]
    return minimum, maximum


def outside_box(point, half):
    return max(0.0, abs(point[0]) - half[0], abs(point[1]) - half[1], abs(point[2]) - half[2])


# --- Shutter, wall-local. Local +Z is the glazed front. ---
shutter_root = new_root("LiftGate_Shutter", COLLECTIONS["00_SHUTTER"])
shutter_mint = Solid("MV_Mint")
shutter_apricot = Solid("MV_Apricot")
shutter_blue = Solid("MV_DomeBlue")

# Outer shell matches the collision box. The centre panel steps back 15 mm,
# which turns the front margin into a border without leaving the box.
shutter_mint.box((-HALF[0], -HALF[1], -HALF[2]), (HALF[0], HALF[1], 0.100))
border = 0.100
shutter_mint.box((-HALF[0], -HALF[1], 0.099), (-HALF[0] + border, HALF[1], HALF[2]))
shutter_mint.box((HALF[0] - border, -HALF[1], 0.099), (HALF[0], HALF[1], HALF[2]))
shutter_mint.box((-HALF[0], -HALF[1], 0.099), (HALF[0], -HALF[1] + border, HALF[2]))
shutter_mint.box((-HALF[0], HALF[1] - border, 0.099), (HALF[0], HALF[1], HALF[2]))
shutter_mint.box(
    (-HALF[0] + border - 0.001, -HALF[1] + border - 0.001, 0.099),
    (HALF[0] - border + 0.001, HALF[1] - border + 0.001, 0.108),
)

for center_x in MEDALLION_X:
    shutter_apricot.extrude_z(half_disk(center_x, 0.624, MEDALLION_RADIUS, False), 0.140, 0.147)
shutter_apricot.box((-1.08, 0.574, 0.140), (1.08, 0.624, 0.147))

for index, center_x in enumerate((-0.36, 0.0, 0.36)):
    diagonal_x = 0.15 if index == 1 else 0.125
    diagonal_y = 0.082 if index == 1 else 0.068
    shutter_blue.extrude_z(
        (
            (center_x + diagonal_x, -0.04),
            (center_x, -0.04 + diagonal_y),
            (center_x - diagonal_x, -0.04),
            (center_x, -0.04 - diagonal_y),
        ),
        0.107,
        0.142,
    )

shutter_objects = [
    commit_object(shutter_mint, "Shutter_Mint", COLLECTIONS["00_SHUTTER"], shutter_root),
    commit_object(shutter_apricot, "Shutter_Apricot", COLLECTIONS["00_SHUTTER"], shutter_root),
    commit_object(shutter_blue, "Shutter_Badge", COLLECTIONS["00_SHUTTER"], shutter_root),
]
mint_excess = max(outside_box(vertex, HALF) for vertex in shutter_mint.vertices)
frieze_excess = max(outside_box(vertex, HALF) for vertex in shutter_apricot.vertices)
badge_excess = max(outside_box(vertex, HALF) for vertex in shutter_blue.vertices)
assert mint_excess < 1e-6, mint_excess
assert frieze_excess <= 0.002 + 1e-6, frieze_excess
assert badge_excess < 1e-6, badge_excess
shutter_bounds = bounds_of(shutter_mint.vertices + shutter_apricot.vertices + shutter_blue.vertices)

# --- Lintel and arch, same wall-local space as the shutter. ---
lintel_root = new_root("LiftGate_FrameLintel", COLLECTIONS["01_FRAME_LINTEL"])
ivory = Solid("MV_Ivory")
apricot = Solid("MV_Apricot")

# Beam sits just above the shutter. Its front is 4 cm behind the shutter face,
# so the sliding panel can pass in front of it.
ivory.box((-1.58, 0.648, -0.22), (1.58, 0.92, 0.100))

arch_center = (0.0, -0.55)
arch_start = math.atan2(0.92 - arch_center[1], -1.38)
arch_end = math.atan2(0.92 - arch_center[1], 1.38)
# atan2 of the left spring is near pi; keep the short upper arc by swapping if needed.
if arch_end < arch_start:
    arch_start, arch_end = arch_end, arch_start
# The springs are left/right and the crown is up, so the arc must pass through +Y.
if arch_start < 0:
    arch_start += 0  # already the upper pair when both angles are in (0, pi)
inner_radius = length((1.38, 0.92 - arch_center[1], 0))
outer_radius = inner_radius + 0.18
ivory.extrude_z(arc_ring(arch_center, inner_radius, outer_radius, arch_start, arch_end, 18), -0.18, 0.08)
trim_radius = outer_radius - 0.045
apricot.extrude_z(arc_ring(arch_center, trim_radius, outer_radius + 0.012, arch_start, arch_end, 18), 0.02, 0.105)

crown = (arch_center[0], arch_center[1] + outer_radius)
apricot.box((crown[0] - 0.09, crown[1] - 0.16, 0.04), (crown[0] + 0.09, crown[1] + 0.05, 0.125))

for center_x in MEDALLION_X:
    apricot.extrude_z(half_disk(center_x, 0.626, MEDALLION_RADIUS, True), 0.128, 0.143)
apricot.box((-1.08, 0.626, 0.128), (1.08, 0.676, 0.143))

lintel_objects = [
    commit_object(ivory, "Frame_Ivory", COLLECTIONS["01_FRAME_LINTEL"], lintel_root),
    commit_object(apricot, "Frame_Apricot", COLLECTIONS["01_FRAME_LINTEL"], lintel_root),
]

# --- Vertical piers. Yaw-local: +Y is world up, +Z is the horizontal front. ---
pier_root = new_root("LiftGate_FramePiers", COLLECTIONS["02_FRAME_PIERS"])
pier_ivory = Solid("MV_Ivory")
pier_apricot = Solid("MV_Apricot")
pier_blue = Solid("MV_DomeBlue")
TICKS = [index * 0.25 for index in range(-6, 7)]


def land_intervals(y0, y1, groove):
    """Solid bands between the 0.25 m snap ticks. The gaps are the incised lines."""
    cursor = y0
    intervals = []
    for tick in TICKS:
        groove_start = tick - groove * 0.5
        groove_end = tick + groove * 0.5
        if groove_end <= y0 or groove_start >= y1:
            continue
        land_end = max(y0, groove_start)
        if land_end - cursor > 0.004:
            intervals.append((cursor, land_end))
        cursor = min(y1, max(cursor, groove_end))
    if y1 - cursor > 0.004:
        intervals.append((cursor, y1))
    return intervals


def add_ticked_shaft(solid, side, y0, y1, hx, z_back, z_front, recess=0.028, groove=0.030):
    """Shaft with a shallow score on the inner face and across the front, every 0.25 m."""
    center = side * PIER_X
    x_inner = center - side * hx
    x_outer = center + side * hx
    x_body = x_inner + side * recess
    # Recessed core. Cladding between the ticks restores the outer surface.
    solid.box(
        (min(x_body, x_outer), y0, -z_back),
        (max(x_body, x_outer), y1, z_front - recess),
    )
    for land_start, land_end in land_intervals(y0, y1, groove):
        solid.box(
            (min(x_inner, x_outer), land_start, z_front - recess - 0.001),
            (max(x_inner, x_outer), land_end, z_front),
        )
        solid.box(
            (min(x_inner, x_body), land_start, -z_back),
            (max(x_inner, x_body), land_end, z_front - recess + 0.001),
        )


def add_finial(solid, center):
    cx, cy, cz = center
    radius = 0.035
    base = []
    for side in range(4):
        turn = math.tau * side / 4 + math.pi / 4
        base.append((cx + radius * math.cos(turn), cy, cz + radius * math.sin(turn)))
    tip = (cx, cy + 0.16, cz)
    start = len(solid.vertices)
    solid.vertices.extend(base)
    solid.vertices.append(tip)
    solid.faces.append(tuple(reversed(range(start, start + 4))))
    for index in range(4):
        nxt = (index + 1) % 4
        solid.faces.append((start + index, start + nxt, start + 4))


# The dome helper above appends rings that are not welded to the previous cap.
# A cleaner dome is built here and the earlier function is replaced before use.
def build_dome(solid, center, radius, height, sides=8, rings=4):
    """Octagonal dome. Rings stop before the pole so the cap is not degenerate."""
    cx, cy, cz = center
    rings_of_points = []
    for ring in range(rings):
        angle = (ring / rings) * (math.pi * 0.5)
        ring_radius = radius * math.cos(angle)
        ring_y = cy + height * math.sin(angle)
        points = []
        for side in range(sides):
            turn = math.tau * side / sides - math.pi / sides
            points.append((cx + ring_radius * math.cos(turn), ring_y, cz + ring_radius * math.sin(turn)))
        rings_of_points.append(points)
    start = len(solid.vertices)
    for ring in rings_of_points:
        solid.vertices.extend(ring)
    apex = (cx, cy + height, cz)
    solid.vertices.append(apex)
    apex_index = len(solid.vertices) - 1
    solid.faces.append(tuple(start + index for index in reversed(range(sides))))
    for ring in range(rings - 1):
        base = start + ring * sides
        above = base + sides
        for side in range(sides):
            nxt = (side + 1) % sides
            solid.faces.append((base + side, base + nxt, above + nxt, above + side))
    top = start + (rings - 1) * sides
    for side in range(sides):
        nxt = (side + 1) % sides
        solid.faces.append((top + side, top + nxt, apex_index))


def add_pier(side):
    center = side * PIER_X
    pier_ivory.box((center - 0.30, -2.39, -0.34), (center + 0.30, -2.18, 0.34))
    pier_apricot.box((center - 0.27, -2.18, -0.31), (center + 0.27, -2.08, 0.31))
    add_ticked_shaft(pier_ivory, side, -2.08, -0.52, 0.205, 0.26, 0.24, 0.042, 0.040)
    pier_apricot.box((center - 0.225, -0.52, -0.275), (center + 0.225, -0.42, 0.255))
    add_ticked_shaft(pier_ivory, side, -0.42, 1.58, 0.175, 0.23, 0.21, 0.042, 0.040)
    pier_apricot.box((center - 0.21, 1.58, -0.25), (center + 0.21, 1.72, 0.24))
    pier_ivory.box((center - 0.16, 1.72, -0.20), (center + 0.16, 1.85, 0.20))
    build_dome(pier_blue, (center, 1.84, 0.0), 0.15, 0.28)
    add_finial(pier_apricot, (center, 2.12, 0.0))


for pier_side in (-1, 1):
    add_pier(pier_side)

pier_objects = [
    commit_object(pier_ivory, "Pier_Ivory", COLLECTIONS["02_FRAME_PIERS"], pier_root),
    commit_object(pier_apricot, "Pier_Apricot", COLLECTIONS["02_FRAME_PIERS"], pier_root),
    commit_object(pier_blue, "Pier_Dome", COLLECTIONS["02_FRAME_PIERS"], pier_root),
]

# Delivery roots carry the unrotated local meshes. Preview copies place them.
for root, task in (
    (shutter_root, "shutter"),
    (lintel_root, "frame-lintel"),
    (pier_root, "frame-piers"),
):
    root["task"] = "E-LiftGate"
    root["part"] = task
    root["units"] = "metres"
    root["unity_axes"] = "+X right, +Y up, +Z forward"


def export_fbx(root, filename):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    path = FBX_DIR / filename
    kwargs = dict(
        filepath=str(path),
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        path_mode="AUTO",
        bake_anim=False,
        use_tspace=False,
    )
    supported = bpy.ops.export_scene.fbx.get_rna_type().properties.keys()
    bpy.ops.export_scene.fbx(**{key: value for key, value in kwargs.items() if key in supported})
    return path


try:
    import addon_utils
    addon_utils.enable("io_scene_fbx")
except Exception as error:  # noqa: BLE001 - export itself reports a missing operator
    print("FBX_ADDON_ENABLE", error)

export_fbx(shutter_root, "LiftGate_Shutter.fbx")
export_fbx(lintel_root, "LiftGate_FrameLintel.fbx")
export_fbx(pier_root, "LiftGate_FramePiers.fbx")


def preview_parent(name, unity_position, unity_quaternion):
    empty = bpy.data.objects.new(name, None)
    empty.empty_display_type = "PLAIN_AXES"
    empty.empty_display_size = 0.25
    COLLECTIONS["90_PREVIEW"].objects.link(empty)
    empty.location = blender_point(unity_position)
    empty.rotation_mode = "QUATERNION"
    empty.rotation_quaternion = blender_quaternion(unity_quaternion)
    return empty


def link_preview(source, parent):
    copy = bpy.data.objects.new(source.name + "_Preview", source.data)
    COLLECTIONS["90_PREVIEW"].objects.link(copy)
    copy.parent = parent
    return copy


shutter_preview = preview_parent("Preview_Shutter", WALL_CENTER, WALL_QUAT)
lintel_preview = preview_parent("Preview_Lintel", WALL_CENTER, WALL_QUAT)
pier_preview = preview_parent("Preview_Piers", WALL_CENTER, YAW_QUAT)
for source in shutter_objects:
    link_preview(source, shutter_preview)
for source in lintel_objects:
    link_preview(source, lintel_preview)
for source in pier_objects:
    link_preview(source, pier_preview)

ground = Solid("Preview_Ground")
ground.box((-6.5, -0.55, -2.5), (4.5, -0.22, 7.5))
ground_root = new_root("Preview_Ground", COLLECTIONS["90_PREVIEW"])
commit_object(ground, "Preview_Foundation", COLLECTIONS["90_PREVIEW"], ground_root)

world = bpy.data.worlds.new("ELiftGate_Sky")
world.use_nodes = True
background = world.node_tree.nodes["Background"]
background.inputs["Color"].default_value = (*linear_color((0.86, 0.78, 0.74)), 1)
background.inputs["Strength"].default_value = 0.85
scene.world = world

sun_data = bpy.data.lights.new("Preview_Sun", "SUN")
sun_data.energy = 3.2
sun_data.angle = math.radians(8)
sun = bpy.data.objects.new("Preview_Sun", sun_data)
COLLECTIONS["90_PREVIEW"].objects.link(sun)
sun.rotation_euler = (math.radians(48), math.radians(18), math.radians(-28))


def make_camera(name, position, target, ortho_scale):
    data = bpy.data.cameras.new(name)
    data.type = "ORTHO"
    data.sensor_fit = "HORIZONTAL"
    data.ortho_scale = ortho_scale
    data.clip_start = 0.05
    data.clip_end = 80
    obj = bpy.data.objects.new(name, data)
    COLLECTIONS["90_PREVIEW"].objects.link(obj)
    obj.location = blender_point(position)
    direction = (blender_point(target) - obj.location).normalized()
    right = direction.cross(Vector((0, 0, 1))).normalized()
    up = right.cross(direction).normalized()
    orientation = Matrix((
        (right.x, up.x, -direction.x),
        (right.y, up.y, -direction.y),
        (right.z, up.z, -direction.z),
    ))
    obj.rotation_euler = orientation.to_euler()
    return obj


focus = (-0.05, 0.95, 0.25)
official_position = (focus[0] + 12, focus[1] + 15, focus[2] - 15)
gate_focus = (-1.55, 2.35, 3.95)
view = normalize((-12, -15, 15))
gate_position = add(gate_focus, view, -18)
official_camera = make_camera("Official", official_position, focus, 19.0 * 16 / 9)
# Vertical ortho size 19 m at 16:9. Blender ortho_scale is the horizontal frame
# when sensor fit is horizontal, so width = height * 16/9.
close_camera = make_camera("Pattern", gate_position, gate_focus, 5.4 * 16 / 9)
look_position = (5.0, 1.5, 5.2)
look_target = (-3.4, 2.7, 0.4)
look_camera = make_camera("Lookback", look_position, look_target, 14.0)

try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    scene.render.engine = "BLENDER_EEVEE"
scene.view_settings.view_transform = "Standard"
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGB"
scene.render.resolution_x = 960
scene.render.resolution_y = 540
scene.render.resolution_percentage = 100


def render_preview(camera, filename):
    scene.camera = camera
    scene.render.filepath = str(QA_DIR / filename)
    bpy.ops.render.render(write_still=True)


# The delivery meshes stay at the local origin. Preview copies carry the placement.
for collection_name in ("00_SHUTTER", "01_FRAME_LINTEL", "02_FRAME_PIERS"):
    for obj in COLLECTIONS[collection_name].objects:
        obj.hide_render = True


render_preview(close_camera, "qa_pattern_aligned.png")
render_preview(official_camera, "qa_official_aligned.png")
shutter_preview.location = blender_point(add(WALL_CENTER, (0, -0.5, 0)))
render_preview(close_camera, "qa_pattern_offset.png")
shutter_preview.location = blender_point(WALL_CENTER)
render_preview(look_camera, "qa_lookback.png")

counts = {
    obj.name: int(obj["triangles"])
    for obj in shutter_objects + lintel_objects + pier_objects
}
manifest = {
    "task": "E-LiftGate",
    "units": "metres, Unity Y up",
    "shutter_anchor": list(WALL_CENTER),
    "frame_anchor": list(FRAME_ANCHOR),
    "orientation_local_position_on_frame": list(ORIENTATION_OFFSET),
    "wall_quaternion_xyzw": list(WALL_QUAT),
    "yaw_quaternion_xyzw": list(YAW_QUAT),
    "wall_axes": {"right": list(WALL_AXES[0]), "up": list(WALL_AXES[1]), "forward": list(WALL_AXES[2])},
    "yaw_axes": {"right": list(YAW_AXES[0]), "up": list(YAW_AXES[1]), "forward": list(YAW_AXES[2])},
    "shutter_half_extents_m": list(HALF),
    "shutter_local_front": "+Z",
    "shutter_mint_max_excess_outside_collision_m": mint_excess,
    "shutter_frieze_max_excess_outside_collision_m": frieze_excess,
    "shutter_badge_max_excess_outside_collision_m": badge_excess,
    "shutter_local_bounds_min": shutter_bounds[0],
    "shutter_local_bounds_max": shutter_bounds[1],
    "corner_match_tolerance_m": 0.002,
    "triangle_counts": counts,
    "triangle_total": sum(counts.values()),
    "materials": ["MV_Ivory", "MV_Apricot", "MV_Mint", "MV_DomeBlue"],
    "medallions": {"radius_m": MEDALLION_RADIUS, "centers_x": list(MEDALLION_X), "seam_local_y": SEAM},
    "pier_centers_local_x": [-PIER_X, PIER_X],
    "tick_world_y": [2.15 + tick for tick in TICKS],
}
(SOURCE_DIR / "E-LiftGate_Manifest.json").write_text(
    json.dumps(manifest, indent=2), encoding="utf-8"
)

blend_path = SOURCE_DIR / "E-LiftGate.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
print("E_LIFTGATE_BUILD_COMPLETE")
print(json.dumps({"triangles": counts, "total": sum(counts.values()), "frieze_excess": frieze_excess}, indent=2))
LOG_FILE.close()
