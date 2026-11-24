"""J-CourtyardDecor: eighteen closed, low-poly, static courtyard props.

Run from any directory (paths are relative to this script):
  blender --background --factory-startup --python-exit-code 1 \
    --python "ArtSource/Prototypes/J-CourtyardDecor/build_j_courtyard_decor.py"
Append -- --render-preview to also render ArtSource/Prototypes/J-CourtyardDecor/preview.png.

All modelling coordinates are UNITY metres: +Y up, +Z forward. Every FBX root is
(0,0,0), with X/Z bounds centred and the lowest Y at zero. The C matrix converts
to Blender Z-up; the FBX exporter uses the same settings as the proven H-Props
builder. Each FBX contains exactly one mesh and no stage, camera, light, texture,
animation, cloth simulation, or armature.
"""
from pathlib import Path
import json
import math
import sys

import bpy
import bmesh
from mathutils import Vector, Matrix

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[2]
FBX_DIR = PROJECT / "Assets" / "Scenes" / "Archive" / "BounceBack" / "Art" / "J-CourtyardDecor" / "FBX"
DOCUMENTATION = PROJECT / "Documentation" / "J-CourtyardDecor_Mesh_Report.json"
SOURCE.mkdir(parents=True, exist_ok=True)
FBX_DIR.mkdir(parents=True, exist_ok=True)
DOCUMENTATION.parent.mkdir(parents=True, exist_ok=True)

# Values are sRGB. Blender shader sockets use scene-linear RGB.
PALETTE = {
    "MV_Ivory": (0.95, 0.88, 0.77),
    "MV_IvoryShade": (0.86, 0.76, 0.70),
    "MV_Rose": (0.74, 0.45, 0.47),
    "MV_RoseDeep": (0.55, 0.30, 0.38),
    "MV_Lilac": (0.50, 0.45, 0.66),
    "MV_Apricot": (0.90, 0.58, 0.37),
    "MV_DomeBlue": (0.30, 0.47, 0.80),
    "MV_Water": (0.45, 0.80, 0.84),
}
USED = list(PALETTE)
ALLOWED = set(USED)
BANNED = {"MV_Mint", "MV_Gold"}
EXPECTED_NAMES = [
    "J_Blue_Wooden_Bench",
    "J_Courtyard_Chair",
    "J_Cafe_Table_Round",
    "J_Ceramic_Stool",
    "J_House_Number_07",
    "J_Courtyard_Wall_Fountain",
    "J_Blue_Railing_1m",
    "J_Rooftop_Pergola_2m",
    "J_Canvas_Parasol",
    "J_Striped_Awning_2m",
    "J_Laundry_Line_2m",
    "J_Clay_Pitcher",
    "J_Fruit_Basket",
    "J_Blue_Wooden_Crate",
    "J_Rope_Coil",
    "J_Lifebuoy",
    "J_Book_Stack",
    "J_Candle_Set",
]


def srgb_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.name = "J-CourtyardDecor_Source"
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1
scene.unit_settings.length_unit = "METERS"
assets = bpy.data.collections.new("01_DELIVERABLE_PROPS")
scene.collection.children.link(assets)
stage = bpy.data.collections.new("90_PREVIEW_ONLY")
scene.collection.children.link(stage)
MATS = {}
for name, color in PALETTE.items():
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    rgba = tuple(srgb_linear(c) for c in color) + (1,)
    mat.diffuse_color = rgba
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = rgba
    shader.inputs["Roughness"].default_value = 0.82
    mat["sRGB"] = list(color)
    MATS[name] = mat

# Unity design basis -> Blender basis. FBX axis_forward=-Z, axis_up=Y converts back.
C = Matrix(((1, 0, 0), (0, 0, -1), (0, 1, 0)))


class Builder:
    """Small mesh builder deliberately matching the H-Props source workflow."""

    def __init__(self):
        self.vertices = []
        self.faces = []
        self.materials = []
        self.smooth = []

    def add(self, verts, faces, mat, smooth=False):
        start = len(self.vertices)
        self.vertices.extend(tuple(C @ Vector(v)) for v in verts)
        self.faces.extend(tuple(i + start for i in f) for f in faces)
        if isinstance(mat, str):
            self.materials.extend([mat] * len(faces))
        else:
            assert len(mat) == len(faces)
            self.materials.extend(mat)
        self.smooth.extend([smooth] * len(faces) if isinstance(smooth, bool) else smooth)

    def finish(self, name):
        assert self.vertices and self.faces, name
        assert set(self.materials) <= ALLOWED, (name, set(self.materials) - ALLOWED)
        assert not (set(self.materials) & BANNED), name
        mesh = bpy.data.meshes.new(name + "_Mesh")
        mesh.from_pydata(self.vertices, [], self.faces)
        mesh.update()
        used_names = list(dict.fromkeys(self.materials))
        assert len(used_names) <= 4, (name, used_names)
        indices = {}
        for i, name_ in enumerate(used_names):
            mesh.materials.append(MATS[name_])
            indices[name_] = i
        for poly, name_, smooth in zip(mesh.polygons, self.materials, self.smooth):
            poly.material_index = indices[name_]
            poly.use_smooth = smooth
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.normal_update()
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()
        mesh.calc_loop_triangles()
        obj = bpy.data.objects.new(name, mesh)
        assets.objects.link(obj)
        normalize_pivot(obj)
        add_uv(obj)
        obj["asset_family"] = "J-CourtyardDecor"
        obj["static_mesh"] = True
        return obj


def normalize_pivot(obj):
    pts = [Vector(v) for v in obj.bound_box]
    low = Vector(tuple(min(p[i] for p in pts) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in pts) for i in range(3)))
    center = (low + high) * 0.5
    center.z = low.z
    for vertex in obj.data.vertices:
        vertex.co -= center
    obj.location = (0, 0, 0)
    obj.rotation_euler = (0, 0, 0)
    obj.scale = (1, 1, 1)
    obj.data.update()


def add_uv(obj):
    uv = obj.data.uv_layers.new(name="UV0_Metric")
    for polygon in obj.data.polygons:
        dominant = max(range(3), key=lambda axis: abs(polygon.normal[axis]))
        axes = ((1, 2), (0, 2), (0, 1))[dominant]
        for index in polygon.loop_indices:
            p = obj.data.vertices[obj.data.loops[index].vertex_index].co
            uv.data[index].uv = (p[axes[0]], p[axes[1]])


# Consistent cyclic quads. The H-Props cap order is not reused verbatim because
# its top/bottom order is self-crossing and fails this pack's closed-mesh audit.
BOX_FACES = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1),
             (1, 5, 7, 3), (3, 7, 6, 2), (2, 6, 4, 0)]


def box(b, center, size, mat):
    cx, cy, cz = center
    x, y, z = [v * 0.5 for v in size]
    assert min(x, y, z) > 0
    verts = [(cx + sx*x, cy + sy*y, cz + sz*z) for sy, sz, sx in
             [(-1,-1,-1),(-1,-1,1),(-1,1,-1),(-1,1,1),
              (1,-1,-1),(1,-1,1),(1,1,-1),(1,1,1)]]
    b.add(verts, BOX_FACES, mat)


def beam(b, a, c, radius, mat, segments=8, radius_end=None):
    a, c = Vector(a), Vector(c)
    assert (c - a).length > 1e-7
    direction = (c - a).normalized()
    helper = Vector((0, 1, 0)) if abs(direction.y) < 0.95 else Vector((0, 0, 1))
    side = direction.cross(helper).normalized()
    up = direction.cross(side).normalized()
    if radius_end is None:
        radius_end = radius
    assert radius > 0 and radius_end > 0
    verts = []
    for center, r in ((a, radius), (c, radius_end)):
        for i in range(segments):
            angle = 2 * math.pi * i / segments
            verts.append(center + r * (math.cos(angle)*side + math.sin(angle)*up))
    faces = [tuple(reversed(range(segments))), tuple(range(segments, 2*segments))]
    for i in range(segments):
        j = (i + 1) % segments
        faces.append((i, j, segments+j, segments+i))
    b.add(verts, faces, mat, [False, False] + [True]*segments)


def lathe(b, profile, mat, segments=12, center=(0, 0, 0), smooth=True):
    """Solid capped lathe; profile entries are (radius, Unity height)."""
    assert len(profile) >= 2 and segments >= 3
    assert all(r > 0 for r, y in profile)
    cx, cy, cz = center
    verts = [(cx+r*math.cos(2*math.pi*i/segments), cy+y,
              cz+r*math.sin(2*math.pi*i/segments))
             for r, y in profile for i in range(segments)]
    faces = []
    for k in range(len(profile)-1):
        for i in range(segments):
            j = (i+1) % segments
            faces.append((k*segments+i, k*segments+j,
                          (k+1)*segments+j, (k+1)*segments+i))
    faces.append(tuple(reversed(range(segments))))
    faces.append(tuple((len(profile)-1)*segments+i for i in range(segments)))
    b.add(verts, faces, mat, smooth)


def ring(b, radius, tube, y, mat, segments=16, sides=5, center=(0, 0, 0)):
    assert radius > tube > 0
    cx, cy, cz = center
    verts = []
    for i in range(segments):
        t = 2*math.pi*i/segments
        for j in range(sides):
            a = 2*math.pi*j/sides
            r = radius + tube*math.cos(a)
            verts.append((cx+r*math.cos(t), cy+y+tube*math.sin(a), cz+r*math.sin(t)))
    faces = []
    for i in range(segments):
        for j in range(sides):
            faces.append((i*sides+j, ((i+1)%segments)*sides+j,
                          ((i+1)%segments)*sides+(j+1)%sides, i*sides+(j+1)%sides))
    b.add(verts, faces, mat, True)


def ico(b, center, size, mat):
    # Closed 20-triangle icosahedron, useful for deliberately faceted fruit.
    phi = (1 + math.sqrt(5)) / 2
    base = [(-1,phi,0),(1,phi,0),(-1,-phi,0),(1,-phi,0),(0,-1,phi),(0,1,phi),
            (0,-1,-phi),(0,1,-phi),(phi,0,-1),(phi,0,1),(-phi,0,-1),(-phi,0,1)]
    faces = [(0,11,5),(0,5,1),(0,1,7),(0,7,10),(0,10,11),(1,5,9),(5,11,4),
             (11,10,2),(10,7,6),(7,1,8),(3,9,4),(3,4,2),(3,2,6),(3,6,8),
             (3,8,9),(4,9,5),(2,4,11),(6,2,10),(8,6,7),(9,8,1)]
    cx, cy, cz = center
    sx, sy, sz = size
    verts = [(cx+p[0]*sx/phi, cy+p[1]*sy/phi, cz+p[2]*sz/phi) for p in base]
    b.add(verts, faces, mat)


def tube_path(b, points, radius, mat, sides=6):
    """One continuous capped tube along a polyline; no open rope/handle ends."""
    points = [Vector(p) for p in points]
    assert len(points) >= 2
    verts = []
    previous_side = None
    for i, point in enumerate(points):
        if i == 0:
            tangent = (points[1] - point).normalized()
        elif i == len(points)-1:
            tangent = (point - points[i-1]).normalized()
        else:
            tangent = (points[i+1] - points[i-1]).normalized()
        if previous_side is None:
            helper = Vector((0, 0, 1)) if abs(tangent.z) < 0.95 else Vector((1, 0, 0))
            side = tangent.cross(helper).normalized()
        else:
            side = previous_side - tangent * previous_side.dot(tangent)
            if side.length < 1e-6:
                helper = Vector((0, 1, 0)) if abs(tangent.y) < 0.95 else Vector((0, 0, 1))
                side = tangent.cross(helper)
            side.normalize()
        up = tangent.cross(side).normalized()
        previous_side = side
        for j in range(sides):
            angle = 2*math.pi*j/sides
            verts.append(point + radius*(math.cos(angle)*side + math.sin(angle)*up))
    faces = [tuple(reversed(range(sides))),
             tuple((len(points)-1)*sides+j for j in range(sides))]
    for i in range(len(points)-1):
        for j in range(sides):
            k = (j+1) % sides
            faces.append((i*sides+j, i*sides+k, (i+1)*sides+k, (i+1)*sides+j))
    b.add(verts, faces, mat, [False, False] + [True]*(len(faces)-2))


def extrude_contour_z(b, contour, z_center, depth, mat):
    """Closed prism from a simple X/Y contour, thickness along Unity Z."""
    n = len(contour)
    assert n >= 3 and depth > 0
    verts = [(x, y, z) for z in (z_center-depth/2, z_center+depth/2) for x, y in contour]
    faces = [tuple(reversed(range(n))), tuple(range(n, 2*n))]
    faces.extend((i, (i+1)%n, (i+1)%n+n, i+n) for i in range(n))
    b.add(verts, faces, mat)


def flat_bar(b, a, c, width, depth, mat):
    """Rectangular geometric stroke in X/Y, extruded in Z (for digit 7/braces)."""
    a, c = Vector(a), Vector(c)
    direction = c-a
    assert direction.length > 1e-7
    perp = Vector((-direction.y, direction.x, 0)).normalized() * (width/2)
    contour = [a-perp, c-perp, c+perp, a+perp]
    z = (a.z+c.z)*0.5
    extrude_contour_z(b, [(p.x, p.y) for p in contour], z, depth, mat)


def arch_contour(radius, spring, bottom, segments=12):
    contour = [(-radius, bottom), (radius, bottom), (radius, spring)]
    contour.extend((radius*math.cos(math.pi*i/segments),
                    spring+radius*math.sin(math.pi*i/segments))
                   for i in range(1, segments+1))
    return contour


def arch_slab(b, radius, spring, bottom, depth, mat, z_center=0, segments=12):
    extrude_contour_z(b, arch_contour(radius, spring, bottom, segments), z_center, depth, mat)


def arch_frame(b, outer, inner, z_front, z_back, mat, segments=12):
    """Closed round-headed ring with a genuine recessed central opening."""
    outer = arch_contour(*outer, segments)
    inner = arch_contour(*inner, segments)
    n = len(outer)
    verts = [(x, y, z) for z in (z_front, z_back) for contour in (outer, inner) for x, y in contour]
    faces = []
    for i in range(n):
        j = (i+1) % n
        faces.append((i, j, n+j, n+i))                         # front ring
        faces.append((2*n+j, 2*n+i, 3*n+i, 3*n+j))             # back ring
        faces.append((i, 2*n+i, 2*n+j, j))                     # outer wall
        faces.append((n+j, 3*n+j, 3*n+i, n+i))                 # inner reveal
    b.add(verts, faces, mat)


def seating(name, chair=False):
    b = Builder()
    width = 0.52 if chair else 1.20
    leg_x = width/2 - 0.065
    depth = 0.46 if chair else 0.44
    # Four ivory legs and under-seat supports.
    for x in (-leg_x, leg_x):
        for z in (-0.15, 0.15):
            box(b, (x, 0.215, z), (0.065, 0.43, 0.065), "MV_Ivory")
        box(b, (x, 0.385, 0), (0.055, 0.065, depth-0.04), "MV_Ivory")
        box(b, (x, 0.69, 0.17), (0.06, 0.58, 0.06), "MV_Ivory")
    for z in (-0.15, 0.15):
        box(b, (0, 0.39, z), (width-0.08, 0.06, 0.055), "MV_Ivory")
    # Separate blue wooden seat slats, with visible gaps.
    count = 3 if chair else 4
    slat_depth = 0.105 if chair else 0.085
    for i in range(count):
        z = (i-(count-1)/2) * (slat_depth+0.018)
        box(b, (0, 0.445, z), (width, 0.045, slat_depth), "MV_DomeBlue")
    # Horizontal blue backrest boards; the open gaps preserve the silhouette.
    for y in (0.64, 0.79, 0.94):
        box(b, (0, y, 0.18), (width, 0.095, 0.04), "MV_DomeBlue")
    if not chair:
        for x in (-leg_x, leg_x):
            box(b, (x, 0.19, 0), (0.045, 0.045, 0.34), "MV_Ivory")
    obj = b.finish(name)
    obj["description"] = "Blue slatted seat/back with ivory legs and supports."
    return obj


def cafe_table():
    b = Builder()
    lathe(b, [(0.24, 0.0), (0.24, 0.04), (0.15, 0.09)], "MV_DomeBlue", 12, smooth=False)
    beam(b, (0, 0.075, 0), (0, 0.70, 0), 0.055, "MV_DomeBlue", 12)
    lathe(b, [(0.38, 0.70), (0.38, 0.75)], "MV_Ivory", 16, smooth=False)
    return b.finish("J_Cafe_Table_Round")


def ceramic_stool():
    b = Builder()
    lathe(b, [(0.17,0),(0.19,0.03),(0.18,0.08),(0.145,0.19),
              (0.16,0.31),(0.19,0.35),(0.19,0.39)], "MV_DomeBlue", 14, smooth=False)
    ring(b, 0.183, 0.012, 0.065, "MV_Ivory", 14, 5)
    ring(b, 0.183, 0.012, 0.335, "MV_Ivory", 14, 5)
    return b.finish("J_Ceramic_Stool")


def house_number():
    b = Builder()
    box(b, (0, 0.17, 0), (0.48, 0.34, 0.045), "MV_DomeBlue")
    # Zero: four ivory rectangular strokes. Seven: top and diagonal block stroke.
    z = -0.031
    x0 = -0.078
    box(b, (x0, 0.247, z), (0.115, 0.026, 0.018), "MV_Ivory")
    box(b, (x0, 0.083, z), (0.115, 0.026, 0.018), "MV_Ivory")
    for x in (x0-0.050, x0+0.050):
        box(b, (x, 0.165, z), (0.026, 0.16, 0.018), "MV_Ivory")
    x7 = 0.078
    box(b, (x7, 0.247, z), (0.12, 0.026, 0.018), "MV_Ivory")
    flat_bar(b, (x7+0.047, 0.237, z), (x7-0.038, 0.077, z), 0.027, 0.018, "MV_Ivory")
    obj = b.finish("J_House_Number_07")
    obj["description"] = "Geometric block digits 07; no font or text dependency."
    return obj


def wall_fountain():
    b = Builder()
    # Front faces Unity -Z. The blue solid panel sits behind a real ivory reveal.
    arch_frame(b, (0.36, 0.78, 0.0), (0.27, 0.78, 0.20), 0.04, 0.16, "MV_Ivory", 12)
    arch_slab(b, 0.27, 0.78, 0.20, 0.025, "MV_DomeBlue", 0.1325, 12)
    beam(b, (0, 0.68, 0.10), (0, 0.68, -0.095), 0.036, "MV_Apricot", 8, 0.025)
    # Compact basin intersects the mounting back, leaving a readable bowl in front.
    lathe(b, [(0.15,0.12),(0.24,0.16),(0.29,0.30),(0.29,0.35)],
          "MV_Ivory", 16, center=(0,0,-0.11), smooth=False)
    ring(b, 0.275, 0.015, 0.35, "MV_Ivory", 16, 5, center=(0,0,-0.11))
    # Closed thin water volume, not a plane and not animated.
    lathe(b, [(0.24,0.350),(0.24,0.362)], "MV_Water", 16,
          center=(0,0,-0.11), smooth=False)
    obj = b.finish("J_Courtyard_Wall_Fountain")
    obj["mounting"] = "Wall-mounted; visible front is Unity -Z."
    obj["water"] = "Closed static MV_Water volume; no simulation or animation."
    return obj


def railing():
    b = Builder()
    for x in (-0.46, 0.46):
        box(b, (x, 0.39, 0), (0.08, 0.78, 0.08), "MV_Ivory")
        box(b, (x, 0.80, 0), (0.08, 0.04, 0.08), "MV_Ivory")
    for y in (0.24, 0.68):
        box(b, (0, y, 0), (0.84, 0.065, 0.055), "MV_DomeBlue")
    for x in (-0.32, -0.16, 0, 0.16, 0.32):
        box(b, (x, 0.46, 0), (0.04, 0.42, 0.045), "MV_DomeBlue")
    obj = b.finish("J_Blue_Railing_1m")
    obj["overall_width_m"] = 1.0
    return obj


def pergola():
    b = Builder()
    for x in (-0.89, 0.89):
        for z in (-0.65, 0.65):
            box(b, (x, 0.91, z), (0.10, 1.82, 0.10), "MV_Ivory")
            box(b, (x, 0.04, z), (0.14, 0.08, 0.14), "MV_Ivory")
    for z in (-0.65, 0.65):
        box(b, (0, 1.82, z), (2.0, 0.12, 0.10), "MV_DomeBlue")
    for x in (-0.89, 0.89):
        box(b, (x, 1.82, 0), (0.10, 0.12, 1.50), "MV_DomeBlue")
    for i in range(9):
        z = -0.68 + i*0.17
        box(b, (0, 1.91, z), (2.0, 0.06, 0.065), "MV_DomeBlue")
    obj = b.finish("J_Rooftop_Pergola_2m")
    obj["overall_width_m"] = 2.0
    obj["description"] = "Four ivory columns and a static blue slatted roof."
    return obj


def parasol():
    b = Builder()
    beam(b, (0,0.04,0), (0,1.96,0), 0.025, "MV_Apricot", 8)
    lathe(b, [(0.12,0),(0.12,0.035),(0.07,0.07)], "MV_Ivory", 10, smooth=False)
    lathe(b, [(0.035,1.96),(0.035,2.005),(0.008,2.035)], "MV_Apricot", 8, smooth=False)
    segments = 12
    radius = 0.72
    verts = [(0,1.98,0), (0,1.91,0)]
    for y in (1.62, 1.59):
        for i in range(segments):
            t = 2*math.pi*i/segments
            verts.append((radius*math.cos(t), y, radius*math.sin(t)))
    faces, mats = [], []
    for i in range(segments):
        j = (i+1) % segments
        mat = "MV_DomeBlue" if i % 2 == 0 else "MV_Ivory"
        faces.append((0, 2+i, 2+j)); mats.append(mat)
        faces.append((1, 2+segments+j, 2+segments+i)); mats.append(mat)
        faces.append((2+i, 2+segments+i, 2+segments+j, 2+j)); mats.append(mat)
    b.add(verts, faces, mats)
    # Six understated underside ribs make the canopy read immediately as a parasol.
    for i in range(0, segments, 2):
        t = 2*math.pi*i/segments
        beam(b, (0,1.90,0), (0.68*math.cos(t),1.585,0.68*math.sin(t)),
             0.008, "MV_Apricot", 5)
    return b.finish("J_Canvas_Parasol")


def awning():
    b = Builder()
    stripes = 8
    for i in range(stripes):
        x0 = -1.0 + 2.0*i/stripes
        x1 = -1.0 + 2.0*(i+1)/stripes
        mat = "MV_DomeBlue" if i % 2 == 0 else "MV_Ivory"
        top = [(x0,1.25,0.12),(x1,1.25,0.12),(x1,0.95,-0.65),(x0,0.95,-0.65)]
        verts = top + [(x,y-0.025,z) for x,y,z in top]
        faces = [(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)]
        b.add(verts, faces, mat)
        box(b, ((x0+x1)/2,0.89,-0.65), (x1-x0,0.12,0.025), mat)
    box(b, (0,1.235,0.12), (2.0,0.055,0.055), "MV_DomeBlue")
    for x in (-0.91,0.91):
        beam(b, (x,0.65,0.10), (x,0.93,-0.62), 0.018, "MV_Apricot", 6)
    obj = b.finish("J_Striped_Awning_2m")
    obj["mounting"] = "Wall-mounted; slopes toward Unity -Z; no animation."
    obj["overall_width_m"] = 2.0
    return obj


def add_cloth(b, center_x, top_y, width, height, mat, phase):
    # A closed 3x3 grid, 8 mm thick, with geometric folds and an uneven hem.
    cols = rows = 3
    verts = []
    for side in (-1, 1):
        for r in range(rows):
            for c in range(cols):
                u = c/(cols-1)
                x = center_x + (u-0.5)*width + 0.018*math.sin(phase+r*0.7)*r/2
                hem = 0.025*math.sin(c*math.pi/2+phase) if r == rows-1 else 0
                y = top_y - height*r/(rows-1) + hem
                z = 0.025*math.sin(u*2*math.pi+phase)*(r/(rows-1)) + side*0.004
                verts.append((x,y,z))
    n = cols*rows
    faces = []
    for r in range(rows-1):
        for c in range(cols-1):
            a = r*cols+c
            q = (a,a+1,a+1+cols,a+cols)
            faces.append(q)
            faces.append(tuple(n+i for i in reversed(q)))
    boundary = list(range(cols))
    boundary += [r*cols+cols-1 for r in range(1,rows)]
    boundary += [(rows-1)*cols+c for c in range(cols-2,-1,-1)]
    boundary += [r*cols for r in range(rows-2,0,-1)]
    for i,a in enumerate(boundary):
        j = boundary[(i+1) % len(boundary)]
        faces.append((a,n+a,n+j,j))
    b.add(verts, faces, mat)


def laundry_line():
    b = Builder()
    points = []
    for i in range(17):
        x = -1.0 + 2.0*i/16
        y = 1.45 - 0.13*(1-x*x)
        points.append((x,y,0))
    for a,c in zip(points, points[1:]):
        beam(b,a,c,0.008,"MV_Apricot",5)
    for i,(x,w,h,mat) in enumerate([(-0.58,0.36,0.48,"MV_DomeBlue"),
                                    (0.0,0.40,0.42,"MV_Ivory"),
                                    (0.58,0.34,0.52,"MV_Rose")]):
        top = 1.45 - 0.13*(1-x*x) - 0.018
        add_cloth(b,x,top,w,h,mat,i*0.8)
        for dx in (-w*0.38,w*0.38):
            box(b,(x+dx,top+0.012,-0.008),(0.018,0.065,0.025),"MV_Apricot")
    obj = b.finish("J_Laundry_Line_2m")
    obj["cord_span_m"] = 2.0
    obj["description"] = "Static sagging cord and three thick folded cloth meshes; no cloth simulation."
    return obj


def clay_pitcher():
    b = Builder()
    lathe(b, [(0.12,0),(0.16,0.03),(0.19,0.17),(0.16,0.29),
              (0.095,0.36),(0.09,0.42),(0.11,0.44)], "MV_Apricot", 12, smooth=False)
    ring(b,0.105,0.010,0.435,"MV_Ivory",12,5)
    lathe(b, [(0.074,0.440),(0.074,0.444)], "MV_RoseDeep", 12, smooth=False)
    # Geometric tapered spout points forward/up, with a dark closed mouth insert.
    beam(b,(0,0.34,-0.095),(0,0.435,-0.255),0.055,"MV_Apricot",6,0.035)
    beam(b,(0,0.435,-0.254),(0,0.437,-0.262),0.025,"MV_RoseDeep",6)
    handle = [(0.115,0.36,0),(0.19,0.39,0),(0.25,0.36,0),(0.275,0.30,0),
              (0.275,0.23,0),(0.25,0.16,0),(0.20,0.12,0),(0.15,0.115,0)]
    tube_path(b,handle,0.024,"MV_Apricot",6)
    return b.finish("J_Clay_Pitcher")


def fruit_basket():
    b = Builder()
    lathe(b,[(0.16,0),(0.19,0.035),(0.22,0.17),(0.22,0.19)],
          "MV_Apricot",12,smooth=False)
    ring(b,0.215,0.016,0.185,"MV_Apricot",12,5)
    for y,r in ((0.055,0.192),(0.115,0.207)):
        ring(b,r,0.008,y,"MV_Ivory",12,4)
    handle = []
    for i in range(13):
        t = math.pi*i/12
        handle.append((0.215*math.cos(t),0.18+0.29*math.sin(t),0))
    tube_path(b,handle,0.014,"MV_Apricot",5)
    fruits = [(-0.10,0.235,-0.07,"MV_Rose"),(0.02,0.245,-0.10,"MV_Apricot"),
              (0.12,0.235,-0.035,"MV_Rose"),(-0.065,0.245,0.065,"MV_Ivory"),
              (0.075,0.25,0.075,"MV_Apricot"),(0.0,0.34,0.0,"MV_Rose")]
    for i,(x,y,z,mat) in enumerate(fruits):
        ico(b,(x,y,z),(0.115,0.11,0.115),mat)
        beam(b,(x,y+0.045,z),(x+0.008,y+0.075,z),0.006,"MV_Apricot",5)
    return b.finish("J_Fruit_Basket")


def crate():
    b = Builder()
    for x in (-0.25,0.25):
        for z in (-0.21,0.21):
            box(b,(x,0.225,z),(0.055,0.45,0.055),"MV_Ivory")
    for y in (0.09,0.225,0.36):
        for z in (-0.22,0.22):
            box(b,(0,y,z),(0.55,0.10,0.035),"MV_DomeBlue")
        for x in (-0.2575,0.2575):
            box(b,(x,y,0),(0.035,0.10,0.405),"MV_DomeBlue")
    box(b,(0,0.025,0),(0.48,0.05,0.38),"MV_DomeBlue")
    flat_bar(b,(-0.20,0.06,-0.243),(0.20,0.39,-0.243),0.045,0.025,"MV_DomeBlue")
    return b.finish("J_Blue_Wooden_Crate")


def rope_coil():
    b = Builder()
    points = []
    turns = 2.45
    count = 55
    for i in range(count):
        t = i/(count-1)
        angle = turns*2*math.pi*t
        radius = 0.04 + 0.145*t
        points.append((radius*math.cos(angle),0.025+0.012*t,radius*math.sin(angle)))
    end = Vector(points[-1])
    points.append(tuple(end + Vector((0.075,0,0.025))))
    tube_path(b,points,0.014,"MV_Apricot",5)
    obj = b.finish("J_Rope_Coil")
    obj["description"] = "Continuous capped spiral rope with a loose tail."
    return obj


def lifebuoy():
    b = Builder()
    segments,sides = 24,6
    radius,tube = 0.245,0.055
    verts = []
    for i in range(segments):
        t = 2*math.pi*i/segments
        radial = Vector((math.cos(t),math.sin(t),0))
        center = Vector((radius*math.cos(t),0.32+radius*math.sin(t),0))
        for j in range(sides):
            a = 2*math.pi*j/sides
            verts.append(center + tube*(math.cos(a)*radial + Vector((0,0,math.sin(a)))))
    faces,mats = [],[]
    rose_arcs = {0,1,6,7,12,13,18,19}
    for i in range(segments):
        for j in range(sides):
            faces.append((i*sides+j,((i+1)%segments)*sides+j,
                          ((i+1)%segments)*sides+(j+1)%sides,i*sides+(j+1)%sides))
            mats.append("MV_Rose" if i in rose_arcs else "MV_Ivory")
    b.add(verts,faces,mats,True)
    obj = b.finish("J_Lifebuoy")
    obj["description"] = "Vertical ivory ring with four integral rose wrap bands."
    return obj


def book_stack():
    b = Builder()
    books = [
        (0.43,0.30,0.060,"MV_DomeBlue",(-0.010,0.000)),
        (0.39,0.28,0.055,"MV_Lilac",(0.018,0.012)),
        (0.42,0.29,0.050,"MV_Rose",(-0.008,-0.010)),
        (0.37,0.27,0.060,"MV_Ivory",(0.022,0.005)),
    ]
    base = 0
    for w,d,h,mat,(dx,dz) in books:
        y = base+h/2
        box(b,(dx,y,dz),(w-0.025,h-0.016,d-0.025),mat)
        for yy in (base+0.004,base+h-0.004):
            box(b,(dx,yy,dz),(w,0.008,d),mat)
        box(b,(dx-w/2+0.009,y,dz),(0.018,h,d),mat)
        # Fine raised cover bands make each monochrome volume legible as a book.
        for zz in (-d*0.34,d*0.34):
            box(b,(dx-w/2-0.001,y,dz+zz),(0.006,h*0.78,0.012),mat)
        base += h+0.004
    obj = b.finish("J_Book_Stack")
    obj["description"] = "Four offset solid low-poly books: blue, lilac, rose, ivory."
    return obj


def candle_set():
    b = Builder()
    lathe(b,[(0.19,0),(0.21,0.018),(0.21,0.035)],"MV_Apricot",16,smooth=False)
    ring(b,0.20,0.012,0.035,"MV_Apricot",16,5)
    for x,z,h,r in [(-0.085,-0.035,0.24,0.040),(0.065,-0.055,0.17,0.045),(0.025,0.095,0.115,0.038)]:
        base = 0.035
        beam(b,(x,base,z),(x,base+h,z),r,"MV_Ivory",10)
        beam(b,(x,base+h,z),(x,base+h+0.022,z),0.005,"MV_RoseDeep",5)
        lathe(b,[(0.010,base+h+0.018),(0.018,base+h+0.040),
                 (0.010,base+h+0.066),(0.002,base+h+0.086)],
              "MV_Apricot",6,center=(x,0,z),smooth=False)
    return b.finish("J_Candle_Set")


MODELS = [
    seating("J_Blue_Wooden_Bench",False),
    seating("J_Courtyard_Chair",True),
    cafe_table(),
    ceramic_stool(),
    house_number(),
    wall_fountain(),
    railing(),
    pergola(),
    parasol(),
    awning(),
    laundry_line(),
    clay_pitcher(),
    fruit_basket(),
    crate(),
    rope_coil(),
    lifebuoy(),
    book_stack(),
    candle_set(),
]
assert [obj.name for obj in MODELS] == EXPECTED_NAMES
assert len({obj.name for obj in MODELS}) == 18


def bounds_unity(obj):
    points = [obj.matrix_world @ v.co for v in obj.data.vertices]
    low = [min(v[i] for v in points) for i in range(3)]
    high = [max(v[i] for v in points) for i in range(3)]
    dimensions = [high[0]-low[0], high[2]-low[2], high[1]-low[1]]
    return low, high, dimensions


def topology_metrics(mesh):
    mesh.calc_loop_triangles()
    zero = []
    minimum = float("inf")
    for triangle in mesh.loop_triangles:
        a,b,c = (mesh.vertices[index].co for index in triangle.vertices)
        area = (b-a).cross(c-a).length*0.5
        minimum = min(minimum,area)
        if area <= 1e-12:
            zero.append(tuple(triangle.vertices))
    bm = bmesh.new()
    bm.from_mesh(mesh)
    boundary = sum(1 for edge in bm.edges if edge.is_boundary)
    non_manifold = sum(1 for edge in bm.edges if not edge.is_manifold)
    bm.free()
    return zero, minimum, boundary, non_manifold


report = {
    "task": "J-CourtyardDecor",
    "model_count": 18,
    "blender_version": bpy.app.version_string,
    "generator": "ArtSource/Prototypes/J-CourtyardDecor/build_j_courtyard_decor.py",
    "units": "metres",
    "coordinate_convention": "Unity +Y up, +Z forward; bottom-bounds-centre pivot",
    "unity_to_blender_C_matrix": [list(row) for row in C],
    "fbx_export_settings": {
        "axis_forward": "-Z", "axis_up": "Y", "apply_unit_scale": True,
        "apply_scale_options": "FBX_SCALE_NONE", "bake_space_transform": False,
        "bake_anim": False, "object_types": ["MESH"], "one_mesh_per_file": True,
    },
    "palette_srgb": PALETTE,
    "allowed_materials": USED,
    "banned_materials_absent": sorted(BANNED),
    "source_blend": "ArtSource/Prototypes/J-CourtyardDecor/J-CourtyardDecor.blend",
    "fbx_directory": "Assets/Scenes/Archive/BounceBack/Art/J-CourtyardDecor/FBX",
    "models": [],
    "contains_external_textures": False,
    "contains_animation": False,
    "contains_cloth_simulation": False,
    "zero_area_triangle_epsilon": 1e-12,
}

for obj in MODELS:
    mesh = obj.data
    low,high,dimensions = bounds_unity(obj)
    triangles = len(mesh.loop_triangles)
    slots = [m.name for m in mesh.materials]
    zero,minimum,boundary,non_manifold = topology_metrics(mesh)
    assert triangles <= 1500, (obj.name,triangles)
    assert len(slots) <= 4, (obj.name,slots)
    assert set(slots) <= ALLOWED and not (set(slots) & BANNED), (obj.name,slots)
    assert abs(low[2]) < 0.000001, (obj.name,low)
    assert abs(low[0]+high[0]) < 0.000002, (obj.name,low,high)
    assert abs(low[1]+high[1]) < 0.000002, (obj.name,low,high)
    assert not zero, (obj.name,zero)
    assert boundary == 0 and non_manifold == 0, (obj.name,boundary,non_manifold)
    assert len(mesh.uv_layers) == 1 and mesh.uv_layers[0].name == "UV0_Metric"
    assert len(mesh.uv_layers[0].data) == len(mesh.loops)
    assert not mesh.shape_keys and obj.animation_data is None
    if obj.name != "J_Courtyard_Wall_Fountain":
        assert "MV_Water" not in slots, obj.name
    record = {
        "name": obj.name,
        "dimensions_unity_xyz_m": dimensions,
        "bounds_unity_local_min": [low[0],low[2],low[1]],
        "bounds_unity_local_max": [high[0],high[2],high[1]],
        "pivot_unity_local": [0,0,0],
        "triangles": triangles,
        "materials": slots,
        "vertices_blender": len(mesh.vertices),
        "faces_blender": len(mesh.polygons),
        "uv_layers": [uv.name for uv in mesh.uv_layers],
        "zero_area_triangles": len(zero),
        "minimum_triangle_area_m2": minimum,
        "boundary_edges": boundary,
        "non_manifold_edges": non_manifold,
        "closed_mesh": True,
        "static": True,
        "fbx": "FBX/"+obj.name+".fbx",
        "fbx_project_path": "Assets/Scenes/Archive/BounceBack/Art/J-CourtyardDecor/FBX/"+obj.name+".fbx",
    }
    report["models"].append(record)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_DIR/(obj.name+".fbx")), use_selection=True,
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE',
        axis_forward='-Z', axis_up='Y', object_types={'MESH'},
        use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
        bake_space_transform=False, path_mode='AUTO')
    obj.select_set(False)

# Preview-only stage, camera and lighting. Exports above happened at the origin.
def move_to_stage(obj):
    for collection in list(obj.users_collection):
        collection.objects.unlink(obj)
    stage.objects.link(obj)


def stage_box(name,center,size,mat):
    b = Builder()
    box(b,center,size,mat)
    obj = b.finish(name)
    move_to_stage(obj)
    obj.location = C @ Vector(center)
    return obj


stage_box("Preview_Plinth",(0,-0.13,0),(14.2,0.26,7.2),"MV_Ivory")
stage_box("Preview_Plinth_Base",(0,-0.30,0),(14.32,0.08,7.32),"MV_Rose")
placements = {}
positions = [(x,0,z) for z in (2.25,0,-2.25) for x in (-5.5,-3.3,-1.1,1.1,3.3,5.5)]
for obj,pos in zip(MODELS,positions):
    obj.location = C @ Vector(pos)
    placements[obj.name] = list(pos)
report["preview_placements_unity"] = placements
report["mounting_notes"] = {
    "J_House_Number_07": "Wall plaque; front -Z; origin at plaque bottom centre.",
    "J_Courtyard_Wall_Fountain": "Wall-mounted; front -Z; origin at arch bottom centre.",
    "J_Striped_Awning_2m": "Wall-mounted; slopes toward -Z; origin at bottom-bounds centre.",
    "J_Laundry_Line_2m": "Two-metre suspended line; no posts; origin at bottom-bounds centre.",
    "J_Lifebuoy": "Vertical display ring in X/Y plane; may be wall hung.",
}

target = C @ Vector((0,0.55,0))
bpy.ops.object.camera_add(location=target + C @ Vector((10,12,-15)))
cam = bpy.context.object
cam.name = "Preview_Camera_Unity_Reference_Angle"
move_to_stage(cam)
cam.rotation_euler = (target-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.type = 'ORTHO'
cam.data.ortho_scale = 16.0
scene.camera = cam
for name,loc,power,size in [("Key",(-4,9,-6),1900,8),("Fill",(6,7,5),1300,7)]:
    bpy.ops.object.light_add(type='AREA',location=C @ Vector(loc))
    light = bpy.context.object
    light.name = "Preview_"+name
    move_to_stage(light)
    light.data.energy = power
    light.data.shape = 'DISK'
    light.data.size = size
    light.rotation_euler = (Vector((0,0,0))-light.location).to_track_quat('-Z','Y').to_euler()
# Blender 5.x exposes Eevee as BLENDER_EEVEE (4.5 used BLENDER_EEVEE_NEXT).
engine_ids = {item.identifier for item in scene.render.bl_rna.properties['engine'].enum_items}
scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engine_ids else 'BLENDER_EEVEE'
scene.render.resolution_x = 1800
scene.render.resolution_y = 1100
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.world = bpy.data.worlds.new("Preview_World")
scene.world.color = (0.25,0.25,0.25)
bpy.ops.object.select_all(action='DESELECT')
bpy.context.view_layer.objects.active = None
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"J-CourtyardDecor.blend"))
if "--render-preview" in sys.argv:
    scene.render.filepath = str(SOURCE/"preview.png")
    bpy.ops.render.render(write_still=True)

# Round-trip every FBX in an empty temporary scene. This does not alter the saved
# source file; it verifies that each delivered file contains only its own mesh.
verify_scene = bpy.data.scenes.new("TEMP_FBX_ROUNDTRIP")
bpy.context.window.scene = verify_scene
for record in report["models"]:
    for obj in list(verify_scene.objects):
        bpy.data.objects.remove(obj,do_unlink=True)
    path = FBX_DIR/(record["name"]+".fbx")
    assert path.is_file() and path.stat().st_size > 0, path
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
    meshes = [obj for obj in verify_scene.objects if obj.type == 'MESH']
    others = [obj for obj in verify_scene.objects if obj.type != 'MESH']
    assert len(meshes) == 1 and not others, (record["name"],len(meshes),[(o.name,o.type) for o in others])
    obj = meshes[0]
    mesh = obj.data
    mesh.calc_loop_triangles()
    low,high,dimensions = bounds_unity(obj)
    zero,minimum,boundary,non_manifold = topology_metrics(mesh)
    slots = [m.name for m in mesh.materials]
    base_slots = [name.rsplit('.',1)[0] if name.rsplit('.',1)[-1].isdigit() else name for name in slots]
    assert len(mesh.loop_triangles) == record["triangles"], (record["name"],len(mesh.loop_triangles),record["triangles"])
    assert all(abs(a-b) < 0.0001 for a,b in zip(dimensions,record["dimensions_unity_xyz_m"])), (record["name"],dimensions)
    assert abs(low[2]) < 0.0001 and abs(low[0]+high[0]) < 0.0002 and abs(low[1]+high[1]) < 0.0002
    assert len(mesh.loop_triangles) <= 1500 and len(slots) <= 4
    assert set(base_slots) == set(record["materials"]), (record["name"],slots)
    assert len(mesh.uv_layers) >= 1 and len(mesh.uv_layers[0].data) == len(mesh.loops)
    assert not zero, (record["name"],"FBX zero-area",zero)
    assert obj.animation_data is None and not mesh.shape_keys
    record["fbx_roundtrip_verified"] = True
    record["fbx_import_mesh_objects"] = len(meshes)
    record["fbx_import_triangles"] = len(mesh.loop_triangles)
    record["fbx_import_dimensions_unity_xyz_m"] = dimensions
    record["fbx_import_uv_layers"] = [uv.name for uv in mesh.uv_layers]
    record["fbx_import_zero_area_triangles"] = len(zero)

report["total_triangles"] = sum(model["triangles"] for model in report["models"])
report["max_triangles_per_prop"] = max(model["triangles"] for model in report["models"])
report["max_triangle_prop"] = max(report["models"],key=lambda model:model["triangles"])["name"]
report["all_fbx_roundtrip_verified"] = all(model.get("fbx_roundtrip_verified") for model in report["models"])
report["validation"] = {
    "exact_names": True,
    "model_count_18": len(MODELS) == 18,
    "max_1500_triangles_each": all(model["triangles"] <= 1500 for model in report["models"]),
    "max_4_material_slots_each": all(len(model["materials"]) <= 4 for model in report["models"]),
    "uv0_present_each": all(bool(model["uv_layers"]) for model in report["models"]),
    "all_source_meshes_closed": all(model["closed_mesh"] for model in report["models"]),
    "zero_area_triangles_total": sum(model["zero_area_triangles"] for model in report["models"]),
    "bottom_y_zero_each": True,
    "xz_bounds_centred_each": True,
    "one_mesh_and_no_other_objects_per_fbx": True,
    "no_textures_animation_or_simulation": True,
    "mv_water_only_in_fountain": True,
    "mv_mint_and_mv_gold_absent": True,
}
payload = json.dumps(report,indent=2,ensure_ascii=False)
(SOURCE/"mesh_report.json").write_text(payload,encoding="utf-8")
DOCUMENTATION.write_text(payload,encoding="utf-8")
print("J_COURTYARD_DECOR_BLENDER_PASS models=%d total_triangles=%d max_triangles=%d" %
      (len(MODELS),report["total_triangles"],report["max_triangles_per_prop"]))
for record in report["models"]:
    print(record["name"],record["triangles"],record["dimensions_unity_xyz_m"],record["materials"])
