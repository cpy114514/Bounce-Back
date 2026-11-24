"""H-Props: thirteen hand-built, low-poly Santorini courtyard props.

Run from any directory (paths are relative to this script):
  blender --background --factory-startup --python-exit-code 1 --python "ArtSource/Prototypes/H-Props/build_hprops.py"
Use the full path to Blender.exe if Blender is not on PATH.

The modelling coordinates are UNITY metres: +Y up, +Z forward. Every exported
root is (0,0,0); the mesh is centred on its lowest bounds in X/Z and starts at
Y=0. Only the coordinate conversion inside the FBX wrapper changes these axes.
The asset FBXs contain only meshes, not the presentation stage or cameras.
"""
from pathlib import Path
import json
import math

import bpy
import bmesh
from mathutils import Vector, Matrix

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[2]
DEST = PROJECT / "Assets" / "Scenes" / "Archive" / "BounceBack" / "Art" / "H-Props"
for folder in (DEST / "FBX", DEST / "Prefabs", DEST / "Materials", DEST / "Preview", DEST / "Documentation"):
    folder.mkdir(parents=True, exist_ok=True)

# Values are sRGB. Blender shader sockets use scene-linear RGB.
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
# Props never use the interactive Mint or ball/goal Gold.
USED = ["MV_Ivory", "MV_IvoryShade", "MV_Rose", "MV_RoseDeep", "MV_Lilac", "MV_Apricot", "MV_DomeBlue"]

def srgb_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
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

# Convert the Unity design coordinate system to Blender Z-up. The inverse
# happens in the FBX importer; this is the same basis as the existing K assets.
C = Matrix(((1, 0, 0), (0, 0, -1), (0, 1, 0)))

class Builder:
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
            self.materials.extend(mat)
        self.smooth.extend([smooth] * len(faces) if isinstance(smooth, bool) else smooth)

    def finish(self, name):
        mesh = bpy.data.meshes.new(name + "_Mesh")
        mesh.from_pydata(self.vertices, [], self.faces)
        mesh.update()
        for name_ in USED:
            mesh.materials.append(MATS[name_])
        # Compress unused slots before export; at most four per model.
        used_names = list(dict.fromkeys(self.materials))
        mesh.materials.clear()
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


def add_uv(obj):
    uv = obj.data.uv_layers.new(name="UV0_Metric")
    for polygon in obj.data.polygons:
        dominant = max(range(3), key=lambda axis: abs(polygon.normal[axis]))
        axes = ((1, 2), (0, 2), (0, 1))[dominant]
        for index in polygon.loop_indices:
            p = obj.data.vertices[obj.data.loops[index].vertex_index].co
            uv.data[index].uv = (p[axes[0]], p[axes[1]])

BOX_FACES = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 3, 7, 5), (3, 2, 6, 7), (2, 0, 4, 6)]

def box(b, center, size, mat):
    cx, cy, cz = center
    x, y, z = [v * 0.5 for v in size]
    verts = [(cx + sx*x, cy + sy*y, cz + sz*z) for sy, sz, sx in
             [(-1,-1,-1),(-1,-1,1),(-1,1,-1),(-1,1,1),(1,-1,-1),(1,-1,1),(1,1,-1),(1,1,1)]]
    b.add(verts, BOX_FACES, mat)


def beam(b, a, c, radius, mat, segments=8, radius_end=None):
    a, c = Vector(a), Vector(c)
    direction = (c - a).normalized()
    helper = Vector((0, 1, 0)) if abs(direction.y) < 0.95 else Vector((0, 0, 1))
    side = direction.cross(helper).normalized()
    up = direction.cross(side).normalized()
    if radius_end is None:
        radius_end = radius
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


def lathe(b, profile, mat, segments=12, center=(0,0,0), smooth=True):
    # profile is (radius, height); always close the bottom/top for solid props.
    cx, cy, cz = center
    verts = [(cx+r*math.cos(2*math.pi*i/segments), cy+y, cz+r*math.sin(2*math.pi*i/segments))
             for r, y in profile for i in range(segments)]
    faces = []
    for k in range(len(profile)-1):
        for i in range(segments):
            j = (i+1) % segments
            faces.append((k*segments+i, k*segments+j, (k+1)*segments+j, (k+1)*segments+i))
    faces.append(tuple(reversed(range(segments))))
    faces.append(tuple((len(profile)-1)*segments+i for i in range(segments)))
    b.add(verts, faces, mat, smooth)


def ring(b, radius, tube, y, mat, segments=16, sides=5, center=(0,0,0)):
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
            faces.append((i*sides+j, ((i+1)%segments)*sides+j, ((i+1)%segments)*sides+(j+1)%sides, i*sides+(j+1)%sides))
    b.add(verts, faces, mat, True)


def ico(b, center, size, mat, seed=0):
    # A 20-triangle, deliberately faceted icosahedral leaf/petal cluster.
    phi = (1 + math.sqrt(5)) / 2
    base = [(-1,phi,0),(1,phi,0),(-1,-phi,0),(1,-phi,0),(0,-1,phi),(0,1,phi),
            (0,-1,-phi),(0,1,-phi),(phi,0,-1),(phi,0,1),(-phi,0,-1),(-phi,0,1)]
    faces = [(0,11,5),(0,5,1),(0,1,7),(0,7,10),(0,10,11),(1,5,9),(5,11,4),(11,10,2),
             (10,7,6),(7,1,8),(3,9,4),(3,4,2),(3,2,6),(3,6,8),(3,8,9),(4,9,5),(2,4,11),
             (6,2,10),(8,6,7),(9,8,1)]
    cx, cy, cz = center
    sx, sy, sz = size
    verts = [(cx+p[0]*sx/phi, cy+p[1]*sy/phi, cz+p[2]*sz/phi) for p in base]
    b.add(verts, faces, mat)


def arch_slab(b, radius, spring, bottom, depth, mat, segments=12):
    # Closed round-headed panel in the X/Y plane, thickness along Z.
    contour = [(-radius, bottom), (radius, bottom), (radius, spring)]
    contour.extend((radius*math.cos(math.pi*i/segments), spring+radius*math.sin(math.pi*i/segments))
                   for i in range(1, segments+1))
    n = len(contour)
    verts = [(x,y,z) for z in (-depth/2, depth/2) for x,y in contour]
    faces = [tuple(reversed(range(n))), tuple(range(n,2*n))]
    faces.extend((i, (i+1)%n, (i+1)%n+n, i+n) for i in range(n))
    b.add(verts, faces, mat)


def bougainvillea(name, square):
    b = Builder()
    if square:
        box(b, (0,0.19,0), (0.44,0.38,0.40), "MV_Apricot")
        box(b, (0,0.405,0), (0.48,0.07,0.44), "MV_Ivory")
        box(b, (0,0.444,0), (0.38,0.008,0.34), "MV_RoseDeep")
    else:
        lathe(b, [(0.17,0),(0.20,0.04),(0.23,0.34),(0.245,0.37),(0.245,0.41),(0.205,0.41)], "MV_Apricot", 12)
        lathe(b, [(0.203,0.411),(0.203,0.417)], "MV_RoseDeep", 12, smooth=False)
    # Airy, stepped sprays rather than a solid green ball.
    sprays = [((0,0.42,0),(-0.07,0.88,0.02)), ((0,0.42,0),(0.18,0.74,-0.05)),
              ((0,0.42,0),(-0.22,0.68,0.09)), ((0,0.42,0),(0.08,0.97,-0.03))]
    for a, c in sprays:
        beam(b, a, c, 0.012, "MV_RoseDeep", 5)
    clusters = [(-0.22,0.65,0.08,0.14),(-0.13,0.77,0.02,0.15),(0.02,0.86,0.02,0.17),
                (0.13,0.75,-0.04,0.14),(0.22,0.66,-0.03,0.13),(0.04,0.99,-0.03,0.13),
                (-0.08,0.69,0.14,0.14),(0.15,0.85,0.06,0.12),(-0.18,0.85,-0.08,0.12)]
    for i, (x,y,z,r) in enumerate(clusters):
        ico(b, (x,y,z), (r,r*0.9,r*0.82), "MV_Rose")
        ico(b, (x+0.045,y-0.055,z+0.035), (r*0.8,r*0.7,r*0.72), "MV_Rose")
    # A few broad pointed leaves, low in the bouquet.
    for x,y,z,sign in [(-0.16,0.54,0, -1),(0.14,0.55,0.04,1),(0,0.61,-0.11,1)]:
        b.add([(0,0.43,0),(x,y,z),(x+0.04*sign,y+0.10,z+0.04),(x-0.04*sign,y+0.07,z-0.03)],
              [(0,1,2),(0,3,1)], "MV_RoseDeep")
    return b.finish(name)


def shutters():
    b = Builder()
    # A pair, each leaf 0.22m wide; front faces Unity -Z for a -Z wall.
    for center in (-0.135,0.135):
        for x in (center-0.105, center+0.105):
            box(b, (x,0.42,0), (0.025,0.84,0.055), "MV_DomeBlue")
        for y in (0.015,0.825):
            box(b, (center,y,0), (0.22,0.03,0.055), "MV_DomeBlue")
        for i in range(10):
            y = 0.067+i*0.077
            # Sloped closed slats, their overlap remains real geometry.
            verts = [(center-0.098,y-0.026,-0.022),(center+0.098,y-0.026,-0.022),
                     (center+0.098,y+0.026,-0.010),(center-0.098,y+0.026,-0.010),
                     (center-0.098,y-0.026,0.016),(center+0.098,y-0.026,0.016),
                     (center+0.098,y+0.026,0.028),(center-0.098,y+0.026,0.028)]
            b.add(verts, BOX_FACES, "MV_DomeBlue")
        # Small visible ivory hinge pins, not a different accent palette.
        for y in (0.16,0.68):
            beam(b, (center-0.125,y,-0.014), (center-0.125,y+0.055,-0.014), 0.009, "MV_Ivory", 6)
    return b.finish("H_Blue_Shutters_Pair")


def pennants():
    b = Builder()
    # Cord end-to-end = 2m in local X. Catenary-like sag and two-sided ribbon.
    points = []
    for i in range(17):
        x = -1+2*i/16
        y = 0.48-0.16*(1-(x*x))
        points.append(Vector((x,y,0.08*math.sin(math.pi*(x+1)/2))))
    for a,c in zip(points,points[1:]):
        beam(b,a,c,0.009,"MV_Apricot",5)
    for i, x in enumerate((-0.72,-0.24,0.24,0.72)):
        y = 0.48-0.16*(1-x*x)
        z = 0.08*math.sin(math.pi*(x+1)/2)
        width = 0.24
        verts = [(x-width/2,y-0.005,z-0.003),(x+width/2,y-0.005,z-0.003),(x+0.025,y-0.32,z+0.025),
                 (x-width/2,y-0.005,z+0.003),(x+width/2,y-0.005,z+0.003),(x+0.025,y-0.32,z+0.031)]
        b.add(verts, [(0,1,2),(5,4,3),(0,3,4,1),(1,4,5,2),(2,5,3,0)], "MV_DomeBlue" if i%2==0 else "MV_Ivory")
    obj = b.finish("H_Bunting_2m")
    obj["cord_span_m"] = 2.0
    obj["description"] = "Static curved rope and four pennants; no cloth simulation."
    return obj


def lantern():
    b = Builder()
    # Origin = bottom of the hanging lantern, not of its mounting bracket.
    beam(b,(0,0.55,0),(0,0.83,0),0.011,"MV_Apricot",6)
    ring(b,0.035,0.009,0.84,"MV_Apricot",10,4)
    lathe(b,[(0.065,0.08),(0.14,0.14),(0.14,0.17)],"MV_Apricot",6,smooth=False)
    lathe(b,[(0.105,0.17),(0.12,0.43),(0.105,0.47)],"MV_Ivory",6,smooth=False)
    for i in range(6):
        a=2*math.pi*i/6
        beam(b,(0.118*math.cos(a),0.15,0.118*math.sin(a)),(0.118*math.cos(a),0.46,0.118*math.sin(a)),0.012,"MV_Apricot",5)
    lathe(b,[(0.15,0.46),(0.15,0.50),(0.055,0.60),(0.025,0.62)],"MV_DomeBlue",6,smooth=False)
    return b.finish("H_Hanging_Lantern")


def bell():
    b=Builder()
    ring(b,0.033,0.009,0.38,"MV_Apricot",10,4)
    lathe(b,[(0.19,0.04),(0.19,0.08),(0.15,0.11),(0.13,0.24),(0.075,0.30),(0.035,0.33)],"MV_Apricot",12,smooth=False)
    beam(b,(0,0.025,0),(0,0.09,0),0.013,"MV_Apricot",6)
    ico(b,(0,0.015,0),(0.026,0.026,0.026),"MV_Apricot")
    return b.finish("H_Copper_Bell")


def chimney():
    b=Builder()
    lathe(b,[(0.20,0),(0.20,0.36),(0.18,0.39)],"MV_Ivory",12,smooth=False)
    lathe(b,[(0.215,0.34),(0.215,0.40),(0.19,0.415)],"MV_Apricot",12,smooth=False)
    lathe(b,[(0.18,0.40),(0.18,0.44),(0.13,0.51),(0.02,0.58)],"MV_DomeBlue",12)
    return b.finish("H_Domed_Chimney")


def cupola():
    b=Builder()
    lathe(b,[(0.25,0),(0.25,0.055)],"MV_Ivory",16,smooth=False)
    lathe(b,[(0.24,0.055),(0.24,0.085),(0.21,0.13),(0.15,0.235),(0.07,0.32),(0.012,0.37)],"MV_DomeBlue",16)
    lathe(b,[(0.032,0.355),(0.032,0.415)],"MV_Apricot",8,smooth=False)
    return b.finish("H_Roof_Cupola")


def gull(name, variant):
    b=Builder()
    # Flight silhouette: beak points -Z; broad white wings have a blue-grey tip.
    ico(b,(0,0.10,0),(0.070,0.055,0.17),"MV_Ivory")
    ico(b,(0,0.125,-0.135),(0.05,0.045,0.060),"MV_Ivory")
    beam(b,(0,0.115,-0.17),(0,0.105,-0.245),0.026,"MV_Apricot",4,0.003)
    beam(b,(0,0.095,0.10),(0,0.10,0.26),0.06,"MV_Ivory",4,0.008)
    # Wing planforms differ in sweep, span and dihedral, not just rotation.
    settings=[(0.44,0.03,0.04),(0.52,0.16,-0.02),(0.47,-0.055,0.11)]
    span,lift,sweep=settings[variant]
    for sign in (-1,1):
        root=(sign*0.035,0.11,-0.04)
        elbow=(sign*span*0.47,0.11+lift*0.52,-0.015+sweep*0.3)
        tip=(sign*span,0.11+lift,0.04+sweep)
        back=(sign*span*0.50,0.105+lift*0.5,0.14+sweep*0.3)
        trailing=(sign*0.06,0.095,0.095)
        verts=[root,elbow,tip,back,trailing]
        # Upper and lower skins with 5mm thickness.
        verts += [(x,y-0.008,z) for x,y,z in verts]
        faces=[(0,1,3,4),(1,2,3),(5,9,8,6),(6,8,7),(0,5,6,1),(1,6,7,2),(2,7,8,3),(3,8,9,4),(4,9,5,0)]
        mats=["MV_Ivory"]*len(faces)
        mats[1]="MV_Lilac"
        mats[6]="MV_Lilac"
        b.add(verts,faces,mats)
    return b.finish(name)


def pot(name, tall):
    b=Builder()
    if tall:
        profile=[(0.13,0),(0.17,0.035),(0.205,0.16),(0.195,0.28),(0.13,0.40),(0.105,0.43),(0.125,0.445),(0.125,0.48),(0.09,0.48),(0.08,0.43)]
        height=0.48
    else:
        profile=[(0.17,0),(0.22,0.04),(0.25,0.16),(0.22,0.28),(0.17,0.33),(0.19,0.345),(0.19,0.385),(0.145,0.385),(0.135,0.34)]
        height=0.385
    lathe(b,profile,"MV_Apricot",12,smooth=False)
    lathe(b,[(0.081 if tall else 0.133,height-0.051),(0.081 if tall else 0.133,height-0.046)],"MV_RoseDeep",12,smooth=False)
    # Fired-clay accent band uses the same Apricot family, no extra palette.
    ring(b,0.204 if tall else 0.246,0.008,0.15,"MV_Ivory",12,4)
    if tall:
        for sign in (-1,1):
            # Two simple open ears under the neck.
            beam(b,(sign*0.15,0.32,0),(sign*0.235,0.30,0),0.020,"MV_Apricot",5)
            beam(b,(sign*0.235,0.30,0),(sign*0.18,0.235,0),0.020,"MV_Apricot",5)
    return b.finish(name)

MODELS = [
    bougainvillea("H_Bougainvillea_Square", True),
    bougainvillea("H_Bougainvillea_Round", False),
    shutters(), pennants(), lantern(), bell(), chimney(), cupola(),
    gull("H_Seagull_Glide",0),gull("H_Seagull_Upstroke",1),gull("H_Seagull_Bank",2),
    pot("H_Terracotta_Amphora",True),pot("H_Terracotta_Bowl",False),
]

report = {"task":"H-Props", "blender_version":bpy.app.version_string, "units":"metres",
          "coordinate_convention":"Unity +Y up, +Z forward; bottom-bounds-centre pivot",
          "palette_srgb":PALETTE,"source_blend":"ArtSource/Prototypes/H-Props/H-Props.blend",
          "models":[],"contains_external_textures":False,"contains_animation":False}
for obj in MODELS:
    mesh=obj.data
    mesh.calc_loop_triangles()
    points=[obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    low=[min(v[i] for v in points) for i in range(3)]
    high=[max(v[i] for v in points) for i in range(3)]
    dimensions_unity=[high[0]-low[0],high[2]-low[2],high[1]-low[1]]
    triangles=len(mesh.loop_triangles)
    slots=[m.name for m in mesh.materials]
    assert triangles <= 1500, (obj.name,triangles)
    assert len(slots) <= 4, (obj.name,slots)
    assert abs(low[2]) < 0.000001, (obj.name,low)
    assert abs(low[0]+high[0]) < 0.000002, (obj.name,low,high)
    assert abs(low[1]+high[1]) < 0.000002, (obj.name,low,high)
    for triangle in mesh.loop_triangles:
        a, b, c = (mesh.vertices[index].co for index in triangle.vertices)
        analytic_area = (b-a).cross(c-a).length * 0.5
        assert analytic_area > 1e-10, (obj.name,tuple(triangle.vertices),analytic_area)
    record={"name":obj.name,"dimensions_unity_xyz_m":dimensions_unity,
            "pivot_unity_local":[0,0,0],"triangles":triangles,"materials":slots,
            "vertices_blender":len(mesh.vertices),"faces_blender":len(mesh.polygons),
            "fbx":"FBX/"+obj.name+".fbx","prefab":"Prefabs/"+obj.name+".prefab"}
    report["models"].append(record)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.export_scene.fbx(filepath=str(DEST/"FBX"/(obj.name+".fbx")),use_selection=True,
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_NONE',axis_forward='-Z',axis_up='Y',
        object_types={'MESH'},use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,
        bake_space_transform=False,path_mode='AUTO')
    obj.select_set(False)

# A Blender overview is a working source preview only; the delivered screenshot
# is rendered from Unity with the official camera direction.
def move_to_stage(obj):
    for collection in list(obj.users_collection):
        collection.objects.unlink(obj)
    stage.objects.link(obj)

def stage_box(name,center,size,mat):
    b=Builder();box(b,center,size,mat)
    obj=b.finish(name);move_to_stage(obj)
    obj.location=C@Vector(center)
    return obj

stage_box("Preview_Plinth",(0,-0.13,0),(6.0,0.26,4.6),"MV_Ivory")
stage_box("Preview_Plinth_Base",(0,-0.30,0),(6.12,0.08,4.72),"MV_Rose")
placements={}
# Props are displayed at real size on a labelled-by-README, non-gameplay plinth.
positions=[(-2.20,0,1.48),(-1.40,0,1.48),(-0.48,0,1.50),(0.67,0,1.50),(1.62,0,1.50),(2.35,0,1.50),
           (-2.12,0,-0.10),(-1.23,0,-0.10),(-0.32,0,-0.10),(0.63,0,-0.10),(1.63,0,-0.10),
           (-0.72,0,-1.54),(0.48,0,-1.54)]
for obj,pos in zip(MODELS,positions):
    obj.location=C@Vector(pos)
    placements[obj.name]=list(pos)

# A small architectural sample supports the wall-mounted props and birds.
# It is preview-only and never included in an FBX or a deliverable prefab.
b=Builder();box(b,(0,0.5,0),(2.8,1,0.12),"MV_IvoryShade")
wall=b.finish("Preview_Wall_Mount");move_to_stage(wall)
wall.location=C@Vector((-1.78,0,1.20))
# At real scale the pair is attached to this small mounting face in Unity too.
for name in ("H_Blue_Shutters_Pair",):
    obj=bpy.data.objects[name]
    obj.location=C@Vector((-1.78,0.10,1.10))

target = C @ Vector((0,0.25,0))
bpy.ops.object.camera_add(location=target + C @ Vector((12,15,-15)))
cam=bpy.context.object
cam.name="Preview_Camera_Unity_Reference_Angle"
move_to_stage(cam)
cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.type='ORTHO';cam.data.ortho_scale=7.3
scene.camera=cam
for name,loc,power,size in [("Key",(-3,8,-5),1250,6),("Fill",(5,5,5),700,5)]:
    bpy.ops.object.light_add(type='AREA',location=C@Vector(loc))
    light=bpy.context.object;light.name="Preview_"+name;move_to_stage(light)
    light.data.energy=power;light.data.shape='DISK';light.data.size=size
    light.rotation_euler=(Vector((0,0,0))-light.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='BLENDER_EEVEE_NEXT'
scene.render.resolution_x=1920;scene.render.resolution_y=1080;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new("Preview_World")
scene.world.color=(0.25,0.25,0.25)
bpy.ops.object.select_all(action='DESELECT')
bpy.context.view_layer.objects.active=None
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"H-Props.blend"))
report["preview_placements_unity"]=placements
report["total_triangles"] = sum(model["triangles"] for model in report["models"])
report["max_triangles_per_prop"] = max(model["triangles"] for model in report["models"])
(DEST/"Documentation"/"H-Props_Mesh_Report.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
(SOURCE/"mesh_report.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
print("HPROPS_BLENDER_PASS models=%d max_triangles=%d"%(len(MODELS),max(m["triangles"] for m in report["models"])))
for record in report["models"]:
    print(record["name"],record["triangles"],record["dimensions_unity_xyz_m"],record["materials"])
