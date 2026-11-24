"""Build Bounce Back K-01 Santorini visual assets.

Run with Blender 5.3:
  blender.exe --background --python build_k01.py

Geometry is authored in Unity-style local coordinates (X right, Y up, Z forward),
then rotated into Blender's right-handed Z-up space. FBX is exported with the
standard -Z forward / Y up axes and verified in Unity separately.
"""
from pathlib import Path
import bpy
import bmesh
import json
import math
import sys
from mathutils import Matrix, Vector, Quaternion

SOURCE_DIR = Path(__file__).resolve().parent
LOG_FILE = (SOURCE_DIR / "blender-build.log").open("w", encoding="utf-8")
sys.stdout = LOG_FILE
sys.stderr = LOG_FILE
PROJECT_DIR = SOURCE_DIR.parents[2]
DELIVERY_DIR = PROJECT_DIR / "Assets" / "Scenes" / "Archive" / "BounceBack" / "Art" / "K-01"
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

COLLECTIONS = {}
for name in ("01_ARCHITECTURE", "02_COURSE", "03_CLIFF", "90_PREVIEW_PROXIES", "91_CAMERAS_LIGHT"):
    collection = bpy.data.collections.new(name)
    scene.collection.children.link(collection)
    COLLECTIONS[name] = collection


def material(name, color, roughness=0.78, metallic=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = (*color, 1.0)
    principled = mat.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = (*color, 1.0)
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Metallic"].default_value = metallic
    return mat


MATERIALS = [
    material("K01_Lime_White", (0.94, 0.92, 0.82)),
    material("K01_Aegean_Blue", (0.20, 0.45, 0.73), roughness=0.64),
    material("K01_Recess_Blue", (0.075, 0.16, 0.24), roughness=0.9),
    material("K01_Warm_Gold", (0.98, 0.63, 0.20), roughness=0.55),
    material("K01_Cliff_Stone", (0.64, 0.66, 0.61), roughness=0.95),
]
WHITE, BLUE, RECESS, GOLD, STONE = range(5)
PROXY_GREEN = material("Preview_Slime_Green", (0.32, 0.73, 0.43), roughness=0.65)
PROXY_AMBER = material("Preview_Ball_Amber", (0.93, 0.43, 0.16), roughness=0.55)
PROXY_SEA = material("Preview_Sea", (0.24, 0.52, 0.65), roughness=0.86)

# Rows above are the Unity-to-Blender basis change: (x, y, z) -> (x, -z, y).
C = Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def blender_point(point):
    return C @ Vector((*point, 1.0))


def blender_rotation(quaternion_xyzw):
    q = Quaternion((quaternion_xyzw[3], quaternion_xyzw[0], quaternion_xyzw[1], quaternion_xyzw[2]))
    matrix = q.to_matrix().to_4x4()
    return (C @ matrix @ C.inverted()).to_quaternion()


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

    def add(self, vertices, faces, mat):
        offset = len(self.vertices)
        self.vertices.extend(tuple(v) for v in vertices)
        self.faces.extend(tuple(index + offset for index in face) for face in faces)
        self.material_indices.extend([mat] * len(faces))

    def box(self, center, size, mat=WHITE, yaw=0.0):
        cx, cy, cz = center
        sx, sy, sz = (value * 0.5 for value in size)
        vertices = [
            (cx + x * sx, cy + y * sy, cz + z * sz)
            for x, y, z in ((-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1),
                            (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1))
        ]
        if yaw:
            vertices = [yaw_point(v, yaw) for v in vertices]
        faces = ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
                 (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7))
        self.add(vertices, faces, mat)

    def prism_xy(self, polygon, z_min, z_max, mat=WHITE):
        count = len(polygon)
        vertices = [(x, y, z_min) for x, y in polygon] + [(x, y, z_max) for x, y in polygon]
        faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
        for index in range(count):
            nxt = (index + 1) % count
            faces.append((index, nxt, nxt + count, index + count))
        self.add(vertices, faces, mat)

    def lathe(self, profile, segments=16, mat=BLUE, center=(0, 0, 0), phase=0.0):
        cx, cy, cz = center
        vertices = []
        rings = []
        for radius, height in profile:
            if radius <= 0.000001:
                rings.append([len(vertices)])
                vertices.append((cx, cy + height, cz))
                continue
            ring = []
            for segment in range(segments):
                angle = phase + 2.0 * math.pi * segment / segments
                ring.append(len(vertices))
                vertices.append((cx + radius * math.cos(angle), cy + height, cz + radius * math.sin(angle)))
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
        self.add(vertices, faces, mat)

    def rock(self, center_xz, top, bottom, top_size, bottom_size, mat=STONE):
        segments = 8
        cx, cz = center_xz
        vertices = []
        variations = (0.94, 1.05, 0.98, 1.08, 0.93, 1.03, 1.08, 0.96)
        rings = ((top, top_size), (bottom + 0.7, bottom_size), (bottom, tuple(v * 0.86 for v in bottom_size)))
        for ring_index, (height, size) in enumerate(rings):
            for segment in range(segments):
                angle = math.pi / 8.0 + 2.0 * math.pi * segment / segments
                variation = variations[(segment + ring_index) % segments]
                vertices.append((cx + math.cos(angle) * size[0] * 0.5 * variation,
                                 height,
                                 cz + math.sin(angle) * size[1] * 0.5 * variation))
        faces = []
        for ring in range(len(rings) - 1):
            for segment in range(segments):
                nxt = (segment + 1) % segments
                faces.append((ring * segments + segment, ring * segments + nxt,
                              (ring + 1) * segments + nxt, (ring + 1) * segments + segment))
        faces.extend((tuple(reversed(range(segments))), tuple((len(rings) - 1) * segments + i for i in range(segments))))
        self.add(vertices, faces, mat)

    def append_transformed(self, other, origin=(0, 0, 0), yaw=0.0):
        vertices = []
        for vertex in other.vertices:
            rotated = yaw_point(vertex, yaw) if yaw else vertex
            vertices.append(tuple(rotated[i] + origin[i] for i in range(3)))
        offset = len(self.vertices)
        self.vertices.extend(vertices)
        self.faces.extend(tuple(index + offset for index in face) for face in other.faces)
        self.material_indices.extend(other.material_indices)


def arch_wall(builder, origin, width, height, depth, opening_width, opening_height,
              bottom=0.0, yaw=0.0, mat=WHITE, segments=8):
    part = MeshBuilder()
    radius = opening_width * 0.5
    spring = bottom + opening_height - radius
    pier_width = (width - opening_width) * 0.5
    part.box((-(radius + pier_width * 0.5), height * 0.5, 0), (pier_width, height, depth), mat)
    part.box(((radius + pier_width * 0.5), height * 0.5, 0), (pier_width, height, depth), mat)
    if bottom > 0.0001:
        part.box((0, bottom * 0.5, 0), (opening_width, bottom, depth), mat)
    for index in range(segments):
        x0 = -radius + opening_width * index / segments
        x1 = -radius + opening_width * (index + 1) / segments
        y0 = spring + math.sqrt(max(0.0, radius * radius - x0 * x0))
        y1 = spring + math.sqrt(max(0.0, radius * radius - x1 * x1))
        part.prism_xy(((x0, y0), (x1, y1), (x1, height), (x0, height)), -depth * 0.5, depth * 0.5, mat)
    builder.append_transformed(part, origin, yaw)


ASSETS = {}


def create_asset(key, root_name, builder, collection, position, rotation=(0, 0, 0, 1), pivot="", target="", notes=""):
    root = bpy.data.objects.new(root_name, None)
    COLLECTIONS[collection].objects.link(root)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.25
    root.location = blender_point(position).to_3d()
    root.rotation_mode = "QUATERNION"
    root.rotation_quaternion = blender_rotation(rotation)
    mesh = bpy.data.meshes.new(root_name + "_Mesh")
    mesh.from_pydata([blender_point(v).to_3d() for v in builder.vertices], [], builder.faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    for mat in MATERIALS:
        mesh.materials.append(mat)
    for polygon, mat_index in zip(mesh.polygons, builder.material_indices):
        polygon.material_index = mat_index
        polygon.use_smooth = False
    uv_layer = mesh.uv_layers.new(name="UV0_MetricBox")
    for polygon in mesh.polygons:
        normal = polygon.normal
        dominant = max(range(3), key=lambda axis: abs(normal[axis]))
        axes = ((1, 2), (0, 2), (0, 1))[dominant]
        for loop_index in polygon.loop_indices:
            vertex = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            uv_layer.data[loop_index].uv = (vertex[axes[0]], vertex[axes[1]])
    obj = bpy.data.objects.new(root_name + "_Geometry", mesh)
    COLLECTIONS[collection].objects.link(obj)
    obj.parent = root
    obj["visual_only"] = True
    obj["contains_colliders"] = False
    root["task"] = "K-01"
    root["units"] = "meters; Unity +Y up +Z forward"
    root["pivot"] = pivot
    root["target"] = target
    minimum = [min(v[i] for v in builder.vertices) for i in range(3)]
    maximum = [max(v[i] for v in builder.vertices) for i in range(3)]
    dimensions = [maximum[i] - minimum[i] for i in range(3)]
    root["dimensions_m"] = dimensions
    ASSETS[key] = {
        "root": root,
        "mesh": obj,
        "builder": builder,
        "file": "FBX/" + root_name + ".fbx",
        "target": target,
        "pivot": pivot,
        "unity_position": list(position),
        "unity_rotation_xyzw": list(rotation),
        "local_bounds_min_m": minimum,
        "local_bounds_max_m": maximum,
        "local_dimensions_m": dimensions,
        "notes": notes,
        "triangles": sum(len(face) - 2 for face in builder.faces),
    }
    return root


# --- Architecture: ground-centered tower and foundation, installation-centered dome. ---
tower = MeshBuilder()
arch_wall(tower, (0, 0, 0.69), 1.6, 2.65, 0.22, 0.78, 1.85, bottom=0.12)
arch_wall(tower, (0, 3.0, 0.69), 1.6, 1.75, 0.22, 0.50, 1.15, bottom=0.25)
arch_wall(tower, (0.69, 3.0, 0), 1.6, 1.75, 0.22, 0.50, 1.15, bottom=0.25, yaw=90)
tower.box((0, 2.6, -0.71), (1.6, 5.2, 0.18))
tower.box((-0.71, 2.6, 0), (0.18, 5.2, 1.6))
tower.box((0.71, 1.5, 0), (0.18, 3.0, 1.6))
tower.box((0, 2.825, 0.69), (1.6, 0.35, 0.22))
tower.box((0.69, 2.825, 0), (0.22, 0.35, 1.6), yaw=90)
tower.box((0, 4.975, 0.69), (1.6, 0.45, 0.22))
tower.box((0.69, 4.975, 0), (0.22, 0.45, 1.6), yaw=90)
tower.box((0, 0.09, 0), (1.72, 0.18, 1.72))
tower.box((0, 2.69, 0), (1.70, 0.12, 1.70))
tower.box((0, 4.80, 0), (1.70, 0.12, 1.70))
tower.box((0, 5.11, 0), (1.74, 0.18, 1.74))
tower.box((0, 1.03, -0.60), (1.10, 1.88, 0.04), RECESS)
tower.box((0, 3.82, -0.60), (0.88, 1.36, 0.04), RECESS)
create_asset("cupola_tower", "K01_Cupola_Tower", tower, "01_ARCHITECTURE", (-2.4, -0.7, -9.9),
             pivot="Ground center; base Y=0", target="Architecture Visuals/Cupola Tower",
             notes="Two open arch walls with recessed blue interior; no route overhang.")

cupola = MeshBuilder()
cupola.lathe(((0.82, 0.0), (0.86, 0.06), (0.82, 0.14), (0.75, 0.32),
              (0.61, 0.53), (0.41, 0.72), (0.17, 0.85), (0.025, 0.88)), segments=16)
cupola.lathe(((0.055, 0.88), (0.055, 1.00)), segments=10, mat=GOLD)
cupola.lathe(((0.02, 1.00), (0.065, 1.04), (0.02, 1.10), (0.0, 1.12)), segments=10, mat=GOLD)
create_asset("blue_cupola", "K01_Blue_Cupola", cupola, "01_ARCHITECTURE", (-2.4, 4.5, -9.9),
             pivot="Dome installation face center; base Y=0", target="Architecture Visuals/Blue Cupola",
             notes="Sixteen-sided low-poly blue dome with a small warm-gold finial.")

foundation = MeshBuilder()
arch_wall(foundation, (0, 0, 0.78), 2.15, 5.9, 0.24, 1.10, 2.50, bottom=0.25)
arch_wall(foundation, (0.955, 2.85, 0), 1.8, 2.55, 0.24, 0.75, 1.65, bottom=0.30, yaw=90)
foundation.box((0, 2.95, -0.80), (2.15, 5.9, 0.20))
foundation.box((-0.975, 2.95, 0), (0.20, 5.9, 1.8))
foundation.box((0.955, 1.425, 0), (0.24, 2.85, 1.8))
foundation.box((0.955, 5.675, 0), (0.24, 0.45, 1.8))
foundation.box((0, 0.125, 0), (2.22, 0.25, 1.88), STONE)
foundation.box((0, 5.81, 0), (2.25, 0.18, 1.90))
foundation.box((0, 1.45, -0.67), (1.48, 2.55, 0.04), RECESS)
foundation.box((0.77, 3.95, 0), (0.04, 1.80, 1.12), RECESS)
create_asset("terrace_foundation", "K01_Terrace_Foundation", foundation, "01_ARCHITECTURE", (0, -1.75, -9.9),
             pivot="Foundation ground center; base Y=0", target="Architecture Visuals/Terrace Foundation",
             notes="Arched structural-looking shell; visual only, existing deck supplies collision.")

# --- Course modules: exact collider-center pivots and exact local dimensions. ---
def deck_builder(size, blue_trim=True):
    sx, sy, sz = size
    builder = MeshBuilder()
    builder.box((0, 0, 0), size, WHITE)
    if blue_trim:
        band_height = min(0.12, sy * 0.24)
        band_y = -sy * 0.5 + min(0.12, sy * 0.28)
        for side in (-1, 1):
            builder.box((side * (sx * 0.5 + 0.006), band_y, 0), (0.018, band_height, sz * 0.92), BLUE)
            builder.box((0, band_y, side * (sz * 0.5 + 0.006)), (sx * 0.92, band_height, 0.018), BLUE)
    return builder


course_specs = [
    ("launch_terrace", "K01_Course_Launch_Terrace", (0, 4.3, -10), (0, 0, 0, 1), (2.4, 1.4, 2.0), "Course/Launch Terrace/VisualRoot"),
    ("stair_1", "K01_Course_Stair_1", (0, 4.715, -8.5), (0, 0, 0, 1), (2.4, 0.45, 1.0), "Course/Stair 1/VisualRoot"),
    ("stair_2", "K01_Course_Stair_2", (0, 4.655, -7.5), (0, 0, 0, 1), (2.4, 0.45, 1.0), "Course/Stair 2/VisualRoot"),
    ("stair_3", "K01_Course_Stair_3", (0, 4.595, -6.5), (0, 0, 0, 1), (2.4, 0.45, 1.0), "Course/Stair 3/VisualRoot"),
    ("smooth_slope", "K01_Course_Smooth_Slope", (0, 3.9700742, -4.0626755), (0.15869997, 0, 0, 0.98732686), (2.4, 0.4, 4.212173), "Course/Smooth Slope/VisualRoot"),
    ("ramp_approach", "K01_Course_Ramp_Approach", (0, 4.61, -6.25), (0, 0, 0, 1), (2.4, 0.42, 0.5), "Course/Ramp Approach/VisualRoot"),
    ("landing_walk", "K01_Course_Landing_Walk", (0, -0.675, 14.2), (0, 0, 0, 1), (2.8, 0.65, 8.2), "Course/Landing Walk/VisualRoot"),
    ("goal_approach", "K01_Course_Goal_Approach", (-3.0, -0.675, 18.4), (0, 0, 0, 1), (3.3, 0.65, 6.4), "Course/Goal Approach/VisualRoot"),
    ("goal_steps_ramp", "K01_Course_Goal_Steps_Ramp", (-5.287648, -0.43142462, 18.4), (-0.06685244, -0.7039395, -0.06685244, 0.70393944), (6.4, 0.4, 1.2218426), "Course/Goal Steps Ramp/VisualRoot"),
    ("goal_terrace", "K01_Course_Goal_Terrace", (-6.95, -0.495, 18.4), (0, 0, 0, 1), (2.2, 0.75, 6.4), "Course/Goal Terrace/VisualRoot"),
]
for key, name, position, rotation, size, target in course_specs:
    create_asset(key, name, deck_builder(size), "02_COURSE", position, rotation,
                 pivot="Collider center; local Y=+height/2 is the exact rolling surface",
                 target=target, notes=f"Main visual box {size[0]} x {size[1]} x {size[2]} m; replace VisualRoot at local (0,0,0), scale (1,1,1).")

# --- Secondary cliffside stair and destination pavilion. ---
side_steps = MeshBuilder()
for index in range(9):
    top = -index * 0.18
    z = index * 0.43 + 0.23
    side_steps.box((0, top - 0.10, z), (0.74, 0.20, 0.46), WHITE)
    side_steps.box((0, top - 0.055, z - 0.234), (0.64, 0.045, 0.012), BLUE)
    side_steps.box((-0.41, top - 0.28, z), (0.12, 0.38, 0.46), WHITE)
create_asset("cliffside_steps", "K01_Cliffside_Steps", side_steps, "03_CLIFF", (-1.66, 4.80, -8.75),
             pivot="First step top surface center", target="Architecture Visuals/Cliffside Steps (new)",
             notes="Outside negative-X edge of the playable slope; no collision and no obstruction above the ball route.")

pavilion = MeshBuilder()
arch_wall(pavilion, (0, 0, 0), 1.85, 2.25, 0.36, 1.05, 1.82, bottom=0.05, segments=8)
pavilion.box((0, 2.29, 0), (1.98, 0.12, 0.52), BLUE)
pavilion.box((0, 0.06, 0), (1.98, 0.12, 0.52), WHITE)
pavilion.box((0, 2.43, 0), (0.12, 0.16, 0.12), GOLD, yaw=45)
create_asset("goal_pavilion", "K01_Goal_Pavilion", pavilion, "01_ARCHITECTURE", (-7.15, -0.12, 21.18), (0, 1, 0, 0),
             pivot="Terrace installation center; base Y=0", target="Architecture Visuals/Goal Pavilion (new)",
             notes="Behind the goal relative to the default camera; frames rather than covers the destination.")

cliffs = MeshBuilder()
cliffs.rock((0, -9.9), -1.77, -4.30, (2.0, 1.65), (3.0, 2.55))
cliffs.rock((0, 14.2), -1.02, -4.30, (2.50, 7.70), (3.35, 8.20))
cliffs.rock((-3.0, 18.4), -1.02, -4.30, (3.0, 6.05), (3.9, 6.75))
cliffs.rock((-6.95, 18.4), -0.89, -4.30, (2.0, 6.10), (3.0, 7.0))
create_asset("cliff_foundations", "K01_Cliff_Foundations", cliffs, "03_CLIFF", (0, 0, 0),
             pivot="Santorini world origin", target="Architecture Visuals/Cliff Foundations (new)",
             notes="Low-poly stone below deck undersides; all rocks remain below collision routes.")

# --- Preview-only proxies retain the existing mechanism dimensions and initial positions. ---
proxy_root = bpy.data.objects.new("PREVIEW_ONLY_Gameplay_Proxies", None)
COLLECTIONS["90_PREVIEW_PROXIES"].objects.link(proxy_root)
proxy_root["not_for_export"] = True


def preview_mesh(name, builder, mats, position=(0, 0, 0), rotation=(0, 0, 0, 1)):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata([blender_point(v).to_3d() for v in builder.vertices], [], builder.faces)
    mesh.update()
    for mat in mats:
        mesh.materials.append(mat)
    for polygon, mat_index in zip(mesh.polygons, builder.material_indices):
        polygon.material_index = mat_index
    obj = bpy.data.objects.new(name, mesh)
    COLLECTIONS["90_PREVIEW_PROXIES"].objects.link(obj)
    obj.parent = proxy_root
    obj.location = blender_point(position).to_3d()
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = blender_rotation(rotation)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    return obj


proxy = MeshBuilder()
proxy.box((0, 0, 0), (1.35, 0.45, 3.6), 0)
preview_mesh("Preview_Slime_1_Initial", proxy, [PROXY_GREEN], (1.5, 0.8, 1.2))
proxy = MeshBuilder()
proxy.box((0, 0, 0), (1.35, 0.45, 3.6), 0)
preview_mesh("Preview_Slime_2_Initial", proxy, [PROXY_GREEN], (-1.5, -0.2, 8.8))
proxy = MeshBuilder()
proxy.box((0, 0, 0), (1.35, 1.45, 0.28), 0)
preview_mesh("Preview_Deflector_Initial", proxy, [MATERIALS[BLUE]], (1.358, 0.4, 15.795), (0, 0.27563736, 0, 0.9612617))
for name, position in (("Preview_Marker_1", (0, 0.575, -0.72)),
                       ("Preview_Marker_2", (0, -0.425, 6.88)),
                       ("Preview_Marker_Deflector", (0, -0.325, 16.05))):
    proxy = MeshBuilder()
    proxy.box((0, 0, 0), (0.26, 0.08, 0.36), 0)
    preview_mesh(name, proxy, [MATERIALS[GOLD]], position)
proxy = MeshBuilder()
proxy.lathe(((0.675, -0.03), (0.675, 0.03)), segments=32, mat=0)
preview_mesh("Preview_Goal_Marker", proxy, [MATERIALS[GOLD]], (-6.65, -0.06, 18.4))
bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=0.32, location=blender_point((0, 5.32, -10.5)).to_3d())
ball = bpy.context.object
ball.name = "Preview_Ball_Release_Point"
for collection in list(ball.users_collection):
    collection.objects.unlink(ball)
COLLECTIONS["90_PREVIEW_PROXIES"].objects.link(ball)
ball.parent = proxy_root
ball.data.materials.append(PROXY_AMBER)
for polygon in ball.data.polygons:
    polygon.use_smooth = False

# Preview sea is deliberately excluded from every FBX export.
sea = MeshBuilder()
sea.box((0, -4.42, 5), (160, 0.12, 160), 0)
preview_mesh("Preview_Sea_Surface", sea, [PROXY_SEA])

# --- Export each asset at the origin, then restore its authored scene transform. ---
def export_fbx(key, data):
    root = data["root"]
    original_location = root.location.copy()
    original_rotation = root.rotation_quaternion.copy()
    root.location = (0, 0, 0)
    root.rotation_quaternion = Quaternion()
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    data["mesh"].select_set(True)
    bpy.context.view_layer.objects.active = data["mesh"]
    output = PROJECT_DIR / "Assets" / "Scenes" / "Archive" / "BounceBack" / "Art" / "K-01" / data["file"]
    kwargs = dict(filepath=str(output), use_selection=True, object_types={"EMPTY", "MESH"},
                  global_scale=1.0, apply_unit_scale=True, axis_forward="-Z", axis_up="Y",
                  use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
                  path_mode="AUTO", bake_anim=False)
    supported = bpy.ops.export_scene.fbx.get_rna_type().properties.keys()
    bpy.ops.export_scene.fbx(**{key: value for key, value in kwargs.items() if key in supported})
    root.location = original_location
    root.rotation_quaternion = original_rotation


for asset_key, asset_data in ASSETS.items():
    export_fbx(asset_key, asset_data)

# --- Camera and lighting: exact default Unity camera pose and orthographic height. ---
def make_camera(name, position, target, ortho_size, aspect):
    data = bpy.data.cameras.new(name)
    data.type = "ORTHO"
    data.sensor_fit = "HORIZONTAL"
    data.ortho_scale = ortho_size * 2.0 * aspect
    data.clip_start = 0.1
    data.clip_end = 500
    obj = bpy.data.objects.new(name, data)
    COLLECTIONS["91_CAMERAS_LIGHT"].objects.link(obj)
    obj.location = blender_point(position).to_3d()
    direction = (blender_point(target).to_3d() - obj.location).normalized()
    # Explicit world-up construction avoids to_track_quat choosing Blender Y as world up.
    right = direction.cross(Vector((0, 0, 1))).normalized()
    up = right.cross(direction).normalized()
    backward = -direction
    orientation = Matrix(((right.x, up.x, backward.x),
                          (right.y, up.y, backward.y),
                          (right.z, up.z, backward.z)))
    obj.rotation_euler = orientation.to_euler()
    obj["unity_position"] = list(position)
    obj["unity_target"] = list(target)
    obj["unity_orthographic_size"] = ortho_size
    return obj


default_camera = make_camera("K01_Default_Unity_Camera", (20, 22, -6), (-1, 1.5, 3.5), 10.6, 16 / 9)
reverse_camera = make_camera("K01_Reverse_From_Goal", (-15, 14, 28), (-0.5, 2.0, 3.0), 12.2, 16 / 9)
world = bpy.data.worlds.new("K01_Sky")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.66, 0.82, 0.91, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.65
scene.world = world
sun_data = bpy.data.lights.new("K01_Sun", "SUN")
sun_data.energy = 2.1
sun_data.angle = math.radians(9)
sun = bpy.data.objects.new("K01_Sun", sun_data)
COLLECTIONS["91_CAMERAS_LIGHT"].objects.link(sun)
sun.rotation_euler = (math.radians(28), math.radians(-24), math.radians(-32))
area_data = bpy.data.lights.new("K01_Soft_Fill", "AREA")
area_data.energy = 1200
area_data.shape = "DISK"
area_data.size = 12
area = bpy.data.objects.new("K01_Soft_Fill", area_data)
COLLECTIONS["91_CAMERAS_LIGHT"].objects.link(area)
area.location = blender_point((4, 15, 8)).to_3d()
area.rotation_euler = (blender_point((-1, 0, 5)).to_3d() - area.location).to_track_quat("-Z", "Y").to_euler()

scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.render.resolution_percentage = 100
try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
scene.view_settings.view_transform = "AgX"
try:
    scene.view_settings.look = "AgX - Medium High Contrast"
except TypeError:
    pass


def render(camera, filename, width, height):
    scene.camera = camera
    camera.data.ortho_scale = camera["unity_orthographic_size"] * 2.0 * (width / height)
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.filepath = str(PREVIEW_DIR / filename)
    bpy.ops.render.render(write_still=True)


# Save the editable source with the default camera active and no selected helper.
bpy.ops.object.select_all(action="DESELECT")
scene.camera = default_camera
scene.render.resolution_x = 1600
scene.render.resolution_y = 900
for screen in bpy.data.screens:
    for area_ui in screen.areas:
        if area_ui.type == "VIEW_3D":
            area_ui.spaces.active.region_3d.view_perspective = "CAMERA"
            area_ui.spaces.active.shading.type = "MATERIAL"
blend_path = SOURCE_DIR / "K01_Santorini_Architecture.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)

render(default_camera, "K01_DefaultCamera_16x9_Blender.png", 1600, 900)
render(default_camera, "K01_DefaultCamera_4x3_Blender.png", 1200, 900)
render(reverse_camera, "K01_ReverseFromGoal_Blender.png", 1600, 900)
scene.camera = default_camera
scene.render.resolution_x = 1600
scene.render.resolution_y = 900
default_camera.data.ortho_scale = 10.6 * 2.0 * (16 / 9)
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)

manifest = {
    "task": "K-01",
    "source_blend": "ArtSource/Kimi/K-01/K01_Santorini_Architecture.blend",
    "units": "meters",
    "unity_axes": "+Y up, +Z forward",
    "visual_only": True,
    "external_textures": [],
    "materials": [mat.name for mat in MATERIALS],
    "default_camera": {"position": [20, 22, -6], "target": [-1, 1.5, 3.5], "orthographic_size": 10.6},
    "assets": {},
}
for key, data in ASSETS.items():
    manifest["assets"][key] = {name: value for name, value in data.items() if name not in {"root", "mesh", "builder"}}
    manifest["assets"][key]["fbx_bytes"] = (PROJECT_DIR / "Assets" / "Scenes" / "Archive" / "BounceBack" / "Art" / "K-01" / data["file"]).stat().st_size
(DOC_DIR / "K01_Dimensions.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
print("K01_BUILD_COMPLETE", len(ASSETS), "FBX assets", sum(item["triangles"] for item in ASSETS.values()), "triangles")
