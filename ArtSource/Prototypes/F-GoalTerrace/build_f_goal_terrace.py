"""Build F-GoalTerrace for Bounce Back from the collision-layout corners.

Run from the project root with Blender 4.5+ / 5.x:
  blender --background --factory-startup --python-exit-code 1 --python ArtSource/Prototypes/F-GoalTerrace/build_f_goal_terrace.py

All geometry is authored in metres relative to Goal Terrace Art
(4.90, 0.83, 4.10). Unity axes are X right, +Y up, +Z forward.
The FBX conversion remains inside each imported model, never on the outer Prefab.
Only static meshes are exported; cameras and lights are source-preview helpers.
"""
from pathlib import Path
import json
import math
import bpy
import bmesh
from mathutils import Matrix, Vector

TASK = "F-GoalTerrace"
SOURCE_DIR = Path(__file__).resolve().parent
PROJECT_DIR = SOURCE_DIR.parents[2]
LAYOUT_PATH = PROJECT_DIR / "ArtSource" / "Santorini Layout" / "Santorini Collision Layout.json"
DELIVERY_DIR = PROJECT_DIR / "Assets" / "Scenes" / "Archive" / "BounceBack" / "Art" / TASK
for folder in (DELIVERY_DIR / "FBX", DELIVERY_DIR / "Prefabs", DELIVERY_DIR / "Materials",
               DELIVERY_DIR / "Preview", DELIVERY_DIR / "Documentation"):
    folder.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.name = "F_Goal_Terrace_Source"
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.unit_settings.length_unit = "METERS"
COLLECTIONS = {}
for name in ("01_TERRACE", "02_CASCADE", "03_PAVILION", "04_RAILING", "05_WATERFALL", "90_PREVIEW_ONLY"):
    collection = bpy.data.collections.new(name)
    scene.collection.children.link(collection)
    COLLECTIONS[name] = collection

ANCHOR = Vector((4.90, 0.83, 4.10))
# Match the proven B-ReleaseTower Unity round trip, including handedness.
C = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
CI = C.inverted()


def bp(local_unity):
    return (C @ Vector((*local_unity, 1.0))).to_3d()


def up(blender_point):
    return (CI @ Vector((*blender_point, 1.0))).to_3d()


def local(point):
    return Vector(point) - ANCHOR


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
    mat["specified_sRGB"] = list(srgb)
    return mat


PALETTE = {
    "MV_Ivory": (0.95, 0.88, 0.77),
    "MV_IvoryShade": (0.86, 0.76, 0.70),
    "MV_Rose": (0.74, 0.45, 0.47),
    "MV_Lilac": (0.50, 0.45, 0.66),
    "MV_Apricot": (0.90, 0.58, 0.37),
    "MV_DomeBlue": (0.30, 0.47, 0.80),
    "MV_Water": (0.45, 0.80, 0.84),
    "MV_Gold": (0.96, 0.70, 0.32),
}
MATS = {name: material(name, color, 0.62 if name == "MV_Water" else 0.76)
        for name, color in PALETTE.items()}
layout = json.loads(LAYOUT_PATH.read_text(encoding="utf-8"))
items = {item["name"]: item for item in layout["items"]}


def point(item, index):
    value = item["corners"][index]
    return Vector((value["x"], value["y"], value["z"]))


def yaw_point(point, degrees):
    angle = math.radians(degrees)
    cosine, sine = math.cos(angle), math.sin(angle)
    x, y, z = point
    return Vector((cosine * x + sine * z, y, -sine * x + cosine * z))


def frame_point(origin, point, yaw=0.0):
    return Vector(origin) + yaw_point(point, yaw)


class MeshBuilder:
    def __init__(self):
        self.vertices = []
        self.faces = []
        self.material_indices = []
        self.smooth = []

    def add(self, vertices, faces, mat=0, smooth=False):
        offset = len(self.vertices)
        self.vertices.extend(tuple(local(vertex)) for vertex in vertices)
        self.faces.extend(tuple(index + offset for index in face) for face in faces)
        self.material_indices.extend(mat if isinstance(mat, (list, tuple)) else [mat] * len(faces))
        self.smooth.extend([smooth] * len(faces))

    def box(self, center, size, mat=0, yaw=0.0):
        center = Vector(center)
        sx, sy, sz = (value * 0.5 for value in size)
        vertices = []
        for x, y, z in ((-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),
                        (-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)):
            vertices.append(center + yaw_point((x*sx, y*sy, z*sz), yaw))
        faces = ((0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7))
        self.add(vertices, faces, mat)

    def prism_xy(self, polygon, z_min, z_max, origin=(0,0,0), yaw=0.0, mat=0):
        count = len(polygon)
        vertices = [frame_point(origin, (x,y,z_min), yaw) for x,y in polygon]
        vertices += [frame_point(origin, (x,y,z_max), yaw) for x,y in polygon]
        faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
        for index in range(count):
            nxt = (index + 1) % count
            faces.append((index, nxt, nxt + count, index + count))
        self.add(vertices, faces, mat)

    def hexa(self, corners, materials):
        bottom = [corners[i] for i in (0,1,5,4)]
        top = [corners[i] for i in (2,3,7,6)]
        faces = ((0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7))
        self.add(bottom + top, faces, materials)

    def lathe(self, profile, center, segments=24, mat=0, smooth=True):
        center = Vector(center)
        vertices, rings = [], []
        for radius, height in profile:
            if radius <= 0.000001:
                rings.append([len(vertices)])
                vertices.append(center + Vector((0,height,0)))
                continue
            ring = []
            for segment in range(segments):
                angle = 2 * math.pi * segment / segments
                ring.append(len(vertices))
                vertices.append(center + Vector((radius*math.cos(angle), height, radius*math.sin(angle))))
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


def arch_ring(builder, origin, inner_radius, outer_radius, spring_y, depth,
              yaw=0.0, mat=0, underside_mat=None, segments=12):
    for index in range(segments):
        a0 = math.pi * index / segments
        a1 = math.pi * (index + 1) / segments
        polygon = [(inner_radius*math.cos(a0), spring_y + inner_radius*math.sin(a0)),
                   (outer_radius*math.cos(a0), spring_y + outer_radius*math.sin(a0)),
                   (outer_radius*math.cos(a1), spring_y + outer_radius*math.sin(a1)),
                   (inner_radius*math.cos(a1), spring_y + inner_radius*math.sin(a1))]
        before = len(builder.faces)
        builder.prism_xy(polygon, -depth*0.5, depth*0.5, origin, yaw, mat)
        if underside_mat is not None:
            # The inner curved soffit is the side face on the inner radial edge.
            builder.material_indices[before + 5] = underside_mat


def arch_panel(builder, origin, radius, bottom, spring_y, depth, yaw, mat, segments=12):
    polygon = [(-radius,bottom),(radius,bottom)]
    for index in range(segments + 1):
        angle = math.pi * index / segments
        polygon.append((radius*math.cos(angle), spring_y + radius*math.sin(angle)))
    builder.prism_xy(polygon, -depth*0.5, depth*0.5, origin, yaw, mat)


def niche(builder, wall_center, yaw, opening_radius=0.15, bottom=0.45, opening_height=0.29):
    normal = yaw_point((0,0,1), yaw)
    spring = bottom + opening_height - opening_radius
    border = 0.06
    # 17.5 mm maximum projection keeps the shallow relief inside the 20 mm tolerance.
    frame_origin = Vector(wall_center) + normal * 0.011
    for side in (-1,1):
        center = frame_point(frame_origin, (side*(opening_radius + border*0.5), (bottom+spring)*0.5, 0), yaw)
        builder.box(center, (border,spring-bottom,0.013), 0, yaw)
    arch_ring(builder, frame_origin, opening_radius, opening_radius+border, spring,
              0.013, yaw, 0, None, 10)
    panel_origin = Vector(wall_center) + normal * 0.006
    arch_panel(builder, panel_origin, opening_radius, bottom, spring, 0.003, yaw, 1, 10)
    sill = frame_point(Vector(wall_center) + normal*0.009, (0,bottom-0.016,0), yaw)
    builder.box(sill, (2*(opening_radius+border)+0.025,0.025,0.018), 2, yaw)


def create_root(name, collection, notes):
    root = bpy.data.objects.new(name, None)
    COLLECTIONS[collection].objects.link(root)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.22
    root["task"] = TASK
    root["anchor_world"] = list(ANCHOR)
    root["pivot"] = "Goal Terrace Art; outer Prefab rotation zero"
    root["visual_only"] = True
    root["contains_colliders"] = False
    root["contains_scripts"] = False
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
    return obj


modules = []

# --- 01. Collision-matched terrace and two-stage shallow arcaded facade. ---
terrace_root = create_root("FGoalTerrace_Terrace", "01_TERRACE",
    "Exact Landing Terrace core. Two shallow cornice retreats and ten lilac arched niches on -Z/+X; maximum projection 18 mm; +X relief ends before the incoming corridor.")
core = MeshBuilder()
core.hexa([point(items["Landing Terrace"], i) for i in range(8)], [1,0,1,1,1,1])
core_obj = create_mesh("Terrace_Collision_Aligned_Core", core,
                       [MATS["MV_Ivory"],MATS["MV_Rose"]], terrace_root, "01_TERRACE")
facade = MeshBuilder()
# Front and side upper friezes, plus two fine setback lines. Nothing touches the rolling top.
facade.box((2.30,0.890,2.891),(3.40,0.12,0.008),0)
facade.box((4.004,0.890,4.2475),(0.008,0.12,2.705),0)
for y,height in ((0.815,0.05),(0.38,0.045)):
    facade.box((2.30,y,2.887),(3.436,height,0.016),2)
    if y > 0.50:
        facade.box((4.008,y,4.2475),(0.016,height,2.705),2)
    else:
        facade.box((4.008,y,4.72),(0.016,height,3.65),2)
for x in (0.98,1.64,2.30,2.96,3.62):
    niche(facade, (x,0,2.895), 180.0)
for z in (3.24,3.77,4.30,4.83,5.36):
    niche(facade, (4.0,0,z), 90.0, opening_radius=0.14, opening_height=0.28)
facade_obj = create_mesh("Terrace_Two_Stage_Arcade", facade,
                         [MATS["MV_Ivory"],MATS["MV_Lilac"],MATS["MV_Apricot"]], terrace_root, "01_TERRACE")
modules.append((terrace_root,[core_obj,facade_obj],"01_TERRACE"))

# --- 02. Exact sloping lip and pressure-plate supporting floor. ---
cascade_root = create_root("FGoalTerrace_Cascade", "02_CASCADE",
    "Exact eight-corner Cascade Lip Ramp and pressure-plate floor. Plate Surface Slot centre is below the 0.78 m raised walking surface.")
lip = MeshBuilder()
lip.hexa([point(items["Cascade Lip Ramp"], i) for i in range(8)], [1,0,1,1,2,1])
lip_obj = create_mesh("Cascade_Lip_Collision_Aligned", lip,
                      [MATS["MV_Ivory"],MATS["MV_Rose"],MATS["MV_Apricot"]], cascade_root, "02_CASCADE")
floor = MeshBuilder()
floor.hexa([point(items["Cascade Pressure Plate Floor"], i) for i in range(8)], [1,0,1,1,1,1])
floor_obj = create_mesh("Cascade_Floor_Collision_Aligned", floor,
                        [MATS["MV_Ivory"],MATS["MV_Rose"]], cascade_root, "02_CASCADE")
apron = MeshBuilder()
apron.box((5.618,0.41,5.24),(0.016,0.11,3.08),2)
apron.box((5.05,0.41,3.692),(1.12,0.11,0.016),2)
apron.box((5.05,0.41,6.788),(1.12,0.11,0.016),2)
apron_obj = create_mesh("Cascade_Apricot_Undercut", apron,
                        [MATS["MV_Rose"],MATS["MV_IvoryShade"],MATS["MV_Apricot"]], cascade_root, "02_CASCADE")
modules.append((cascade_root,[lip_obj,floor_obj,apron_obj],"02_CASCADE"))

# --- 03. Small diagonal destination pavilion, behind the actual goal. ---
pavilion_root = create_root("FGoalTerrace_Pavilion", "03_PAVILION",
    "Open front/rear arches face the incoming B point (4.6,6.6). Four slender feet follow terrace/ramp/cornice support heights. This is a destination backdrop, not a claim that the ball rolls through its centre.")
PAV_CENTER = Vector((4.90,0.78,4.23))
PAV_ORIGIN = Vector((PAV_CENTER.x,0.0,PAV_CENTER.z))
PAV_YAW = math.degrees(math.atan2(4.60-PAV_CENTER.x,6.60-PAV_CENTER.z))
pav = MeshBuilder()


def support_y(world_x):
    if world_x <= 4.0:
        return 0.96
    if world_x < 4.49:
        return 0.96 + (world_x-4.0) * (-0.18/0.49)
    if world_x <= 5.61:
        return 0.78
    return 0.585


# Wedge-cut feet follow the real support planes rather than hovering over the lip.
for u in (-0.855,0.855):
    for v in (-0.20,0.20):
        vertices = []
        for top in (False,True):
            for du,dv in ((-0.055,-0.055),(0.055,-0.055),(0.055,0.055),(-0.055,0.055)):
                world = frame_point(PAV_ORIGIN,(u+du,0,v+dv),PAV_YAW)
                world.y = 1.63 if top else support_y(world.x)
                vertices.append(world)
        pav.add(vertices, ((0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)), 0)
        capital = frame_point(PAV_ORIGIN,(u,1.60,v),PAV_YAW)
        pav.box(capital,(0.14,0.06,0.14),1,PAV_YAW)
# A real barrel soffit links the two visible arch faces.
arch_ring(pav, PAV_ORIGIN, 0.79,0.86,1.63,0.52,PAV_YAW,0,None,14)
for v in (-0.20,0.20):
    origin = frame_point(PAV_ORIGIN,(0,0,v),PAV_YAW)
    arch_ring(pav,origin,0.79,0.91,1.63,0.12,PAV_YAW,0,None,14)
pav.box(frame_point(PAV_ORIGIN,(0,2.53,0),PAV_YAW),(1.94,0.06,0.66),1,PAV_YAW)
pav.box(frame_point(PAV_ORIGIN,(0,2.58,0),PAV_YAW),(1.98,0.08,0.70),0,PAV_YAW)
pav.lathe([(0.48,0.0),(0.48,0.10)],frame_point(PAV_ORIGIN,(0,2.62,0),PAV_YAW),24,1,False)
pav.lathe([(0.44,0.0),(0.43,0.07),(0.36,0.17),(0.24,0.25),(0.10,0.30),(0.0,0.32)],
          frame_point(PAV_ORIGIN,(0,2.72,0),PAV_YAW),24,2,True)
pav.lathe([(0.045,0.0),(0.045,0.035),(0.0,0.080)],
          frame_point(PAV_ORIGIN,(0,3.04,0),PAV_YAW),12,3,True)
pav_obj = create_mesh("Pavilion_Open_Arch_And_Blue_Dome",pav,
                      [MATS["MV_Ivory"],MATS["MV_Apricot"],MATS["MV_DomeBlue"],MATS["MV_Gold"]],
                      pavilion_root,"03_PAVILION")
modules.append((pavilion_root,[pav_obj],"03_PAVILION"))

# --- 04. Short north railing and two empty planter plinths. ---
railing_root = create_root("FGoalTerrace_Railing", "04_RAILING",
    "Short +Z balustrade ending before the incoming corridor. Two bare plinths only; plants belong to task H.")
rail = MeshBuilder()
for x in (0.78,1.28,1.78,2.28,2.74):
    rail.box((x,1.17,6.45),(0.10,0.42,0.12),0)
    rail.box((x,0.985,6.45),(0.14,0.05,0.14),2)
rail.box((1.76,1.41,6.45),(2.08,0.10,0.12),1)
rail.box((1.76,1.12,6.45),(1.96,0.055,0.07),0)
for x,z in ((0.95,6.18),(2.05,6.20)):
    rail.box((x,1.02,z),(0.30,0.12,0.30),2)
    rail.box((x,1.10,z),(0.24,0.04,0.24),0)
rail_obj = create_mesh("Railing_Short_North_And_Empty_Plinths",rail,
                       [MATS["MV_Ivory"],MATS["MV_Apricot"],MATS["MV_Rose"]],railing_root,"04_RAILING")
modules.append((railing_root,[rail_obj],"04_RAILING"))

# --- 05. Existing eave profile, integrated supports, arched mouth and static water. ---
waterfall_root = create_root("FGoalTerrace_Waterfall", "05_WATERFALL",
    "WaterfallOutlet Stone Eave section reused at the exact slot, rotated to +X and narrowed to 1.04 m. A projecting arch clears the goal-side 0.6 m envelope. Static water only.")
stone = MeshBuilder()
SLOT = Vector((5.72,0.43,5.24))
profile = [(0.0,0.0),(-0.70,0.0),(-0.76,-0.06),(-0.76,-0.22),(-0.68,-0.30),
           (-0.10,-0.30),(0.0,-0.20),(0.0,-0.11),(-0.025,-0.095),(-0.025,-0.065),(0.0,-0.05)]
count = len(profile)
vertices = []
for x in (-0.52,0.52):
    for z,y in profile:
        vertices.append(frame_point(SLOT,(x,y,z),90.0))
faces = [tuple(reversed(range(count))),tuple(range(count,count*2))]
for index in range(count):
    nxt = (index+1)%count
    faces.append((index,nxt,nxt+count,index+count))
stone.add(vertices,faces,[0]*len(faces))
# Split supports leave a genuine water gap from Z 4.70 to 5.78.
for z in (4.39,6.09):
    stone.box((5.64,0.46,z),(0.36,0.25,0.62),0)
# The outlet front projects beyond the collision edge; its closest face is X=5.96.
OUTLET = Vector((6.02,0,5.24))
for u in (-0.60,0.60):
    stone.box(frame_point(OUTLET,(u,0.11,0),90.0),(0.12,0.14,0.12),0,90.0)
arch_ring(stone,OUTLET,0.54,0.66,0.18,0.12,90.0,0,1,14)
stone.box(frame_point(OUTLET,(0,0.855,0),90.0),(1.48,0.07,0.14),2,90.0)
stone_obj = create_mesh("Waterfall_Reused_Eave_And_Arch",stone,
                        [MATS["MV_Ivory"],MATS["MV_IvoryShade"],MATS["MV_Apricot"]],waterfall_root,"05_WATERFALL")

water = MeshBuilder()
rows = [(5.68,0.43,1.02),(5.90,0.43,1.04),(6.03,0.40,1.02),(6.12,0.31,1.00),
        (6.15,0.10,0.96),(6.19,-0.45,0.92),(6.23,-1.05,0.88),(6.29,-1.75,0.82)]
columns = (-0.5,-0.25,0.0,0.25,0.5)
fold = (0.0,0.018,-0.008,0.014,0.0)
front = []
for row,(x,y,width) in enumerate(rows):
    for column,fold_amount in zip(columns,fold):
        front.append(Vector((x+fold_amount*(0 if row<2 else 1),y,5.24+column*width)))
n = len(front)
vertices = front + [vertex-Vector((0.018,0,0)) for vertex in front]
faces = []
for row in range(len(rows)-1):
    for col in range(len(columns)-1):
        a = row*len(columns)+col
        faces.append((a,a+len(columns),a+len(columns)+1,a+1))
        faces.append((a+n,a+1+n,a+len(columns)+1+n,a+len(columns)+n))
# Close the perimeter of the thin water solid.
perimeter = list(range(len(columns)))
perimeter += [row*len(columns)+len(columns)-1 for row in range(1,len(rows))]
perimeter += [(len(rows)-1)*len(columns)+col for col in range(len(columns)-2,-1,-1)]
perimeter += [row*len(columns) for row in range(len(rows)-2,0,-1)]
for i,a in enumerate(perimeter):
    b = perimeter[(i+1)%len(perimeter)]
    faces.append((a,b,b+n,a+n))
water.add(vertices,faces,0)
# Sparse, shallow closed foam strips are geometry, not textures or particles.
for column_index,start_row,end_row in ((1,1,5),(3,3,7),(2,2,4)):
    strip_front = []
    for row in range(start_row,end_row+1):
        x,y,width = rows[row]
        x += fold[column_index]*(0 if row<2 else 1)
        z = 5.24+columns[column_index]*width
        strip_front.extend([Vector((x+0.003,y,z-0.012)),Vector((x+0.003,y,z+0.012))])
    count_strip = len(strip_front)
    verts = strip_front + [v-Vector((0.003,0,0)) for v in strip_front]
    strip_faces = []
    for index in range(0,count_strip-2,2):
        strip_faces.extend([(index,index+2,index+3,index+1),
                            (index+count_strip,index+count_strip+1,index+count_strip+3,index+count_strip+2)])
    strip_faces.extend([(0,1,1+count_strip,count_strip),
                        (count_strip-2,2*count_strip-2,2*count_strip-1,count_strip-1)])
    for i in range(0,count_strip-2,2):
        strip_faces.extend([(i,i+count_strip,i+count_strip+2,i+2),
                            (i+1,i+3,i+count_strip+3,i+count_strip+1)])
    water.add(verts,strip_faces,1)
water_obj = create_mesh("Waterfall_Static_Water_And_Foam",water,
                        [MATS["MV_Water"],MATS["MV_Ivory"]],waterfall_root,"05_WATERFALL")
modules.append((waterfall_root,[stone_obj,water_obj],"05_WATERFALL"))

# --- Source topology, dimensions and exact collision-corner validation. ---
bpy.context.view_layer.update()
manifest = {
    "task": TASK,
    "source_blend": "ArtSource/Prototypes/F-GoalTerrace/F-GoalTerrace.blend",
    "rebuild_script": "ArtSource/Prototypes/F-GoalTerrace/build_f_goal_terrace.py",
    "unity_build_script": "ArtSource/Prototypes/F-GoalTerrace/unity_build_preview.cs",
    "collision_layout": "ArtSource/Santorini Layout/Santorini Collision Layout.json",
    "anchor_unity_world": list(ANCHOR),
    "pivot": "Goal Terrace Art anchor; all five module roots and the combined Prefab use zero local offset/rotation",
    "units": "metres; Unity +Y up, +Z forward",
    "material_srgb": PALETTE,
    "contains_colliders": False,
    "contains_scripts": False,
    "external_textures": [],
    "modules": {},
}
all_world = []


def topology(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    world_points = [up(vertex.co) + ANCHOR for vertex in obj.data.vertices]
    local_points = [up(vertex.co) for vertex in obj.data.vertices]
    world_min = [min(point[i] for point in world_points) for i in range(3)]
    world_max = [max(point[i] for point in world_points) for i in range(3)]
    result = {"vertices":len(bm.verts),
              "triangles":sum(len(face.verts)-2 for face in bm.faces),
              "material_slots":len(obj.data.materials),
              "bounds_local_min":[min(point[i] for point in local_points) for i in range(3)],
              "bounds_local_max":[max(point[i] for point in local_points) for i in range(3)],
              "bounds_world_min":world_min,
              "bounds_world_max":world_max,
              "dimensions_world":[world_max[i]-world_min[i] for i in range(3)],
              "non_manifold_edges":sum(not edge.is_manifold for edge in bm.edges),
              "zero_area_faces":sum(face.calc_area()<1e-10 for face in bm.faces)}
    if result["zero_area_faces"]:
        print("ZERO_AREA_DEBUG", obj.name, [(face.index, [tuple(v.co) for v in face.verts], face.calc_area())
              for face in bm.faces if face.calc_area() < 1e-10])
    bm.free()
    assert result["non_manifold_edges"] == 0, (obj.name,result)
    assert result["zero_area_faces"] == 0, (obj.name,result)
    assert result["material_slots"] <= 4, (obj.name,result)
    return result


def corner_error(obj,item_name):
    maximum = 0.0
    for index in range(8):
        target = bp(local(point(items[item_name],index)))
        nearest = min((vertex.co-target).length for vertex in obj.data.vertices)
        maximum = max(maximum,nearest)
    return maximum


alignment = {}
for root,meshes,collection in modules:
    entry = {"fbx":f"FBX/{root.name}.fbx","meshes":{},"triangles":0}
    for obj in meshes:
        entry["meshes"][obj.name] = topology(obj)
        entry["triangles"] += entry["meshes"][obj.name]["triangles"]
        for vertex in obj.data.vertices:
            all_world.append(up(vertex.co)+ANCHOR)
    entry["bounds_world_min"] = [min(mesh["bounds_world_min"][i] for mesh in entry["meshes"].values()) for i in range(3)]
    entry["bounds_world_max"] = [max(mesh["bounds_world_max"][i] for mesh in entry["meshes"].values()) for i in range(3)]
    entry["dimensions_world"] = [entry["bounds_world_max"][i]-entry["bounds_world_min"][i] for i in range(3)]
    assert entry["triangles"] <= 15000, (root.name,entry["triangles"])
    manifest["modules"][root.name] = entry
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    kwargs = dict(filepath=str(DELIVERY_DIR/"FBX"/(root.name+".fbx")),use_selection=True,
                  object_types={"EMPTY","MESH"},global_scale=1.0,apply_unit_scale=True,
                  axis_forward="-Z",axis_up="Y",use_mesh_modifiers=True,mesh_smooth_type="FACE",
                  add_leaf_bones=False,path_mode="AUTO",bake_anim=False)
    supported = bpy.ops.export_scene.fbx.get_rna_type().properties.keys()
    bpy.ops.export_scene.fbx(**{key:value for key,value in kwargs.items() if key in supported})

for obj,item_name in ((core_obj,"Landing Terrace"),(lip_obj,"Cascade Lip Ramp"),(floor_obj,"Cascade Pressure Plate Floor")):
    error = corner_error(obj,item_name)
    alignment[item_name] = {"max_corner_to_nearest_mesh_vertex_m":error,"mesh":obj.name}
    assert error <= 0.00001, (item_name,error)
manifest["alignment"] = {"tolerance_m":0.02,"targets":alignment,
                         "maximum_facade_projection_m":0.018,
                         "note":"Collision cores are exact. Niche/cornice relief projects no more than 18 mm and stays off the rolling tops."}
manifest["pavilion"] = {"center_world":list(PAV_CENTER),"yaw_degrees":PAV_YAW,
                        "column_center_span_m":1.71,"clear_opening_width_m":1.58,
                        "spring_world_y":1.63,"inner_crown_world_y":2.42,
                        "clear_height_above_raised_plate_m":1.64,
                        "dome_top_world_y":3.04,"finial_top_world_y":3.12,
                        "role":"Open destination backdrop facing the incoming B point; no assumed through-roll"}
manifest["railing"] = {"north_segment_world_x":[0.72,2.80],"z":6.45,"top_world_y":1.46,
                       "empty_planter_centers":[[0.95,1.02,6.18],[2.05,1.02,6.20]]}
manifest["waterfall"] = {"slot_world":list(SLOT),"eave_width_m":1.04,
                         "arch_front_min_world_x":5.96,"drop_bottom_world_y":-1.75,
                         "static_water":True}
manifest["bounds_world"] = {"min":[min(v[i] for v in all_world) for i in range(3)],
                            "max":[max(v[i] for v in all_world) for i in range(3)]}
assert abs(manifest["bounds_world"]["max"][1] - 3.12) < 0.00001, manifest["bounds_world"]
manifest["total_triangles"] = sum(entry["triangles"] for entry in manifest["modules"].values())
manifest["preview_scene"] = "Preview/FGoalTerrace_Preview.unity"
manifest["screenshots"] = ["Preview/FGoalTerrace_Official_1920x1080.png",
                            "Preview/FGoalTerrace_ReverseFromGoal_1920x1080.png"]
(DELIVERY_DIR/"Documentation"/"FGoalTerrace_Geometry.json").write_text(json.dumps(manifest,indent=2),encoding="utf-8")

# Source-only camera and light rig. No proxy mesh is saved in the delivery collections.
def camera_object(name,position,target,ortho_scale=None):
    data = bpy.data.cameras.new(name)
    data.type = "ORTHO" if ortho_scale else "PERSP"
    if ortho_scale:
        data.ortho_scale = ortho_scale
    else:
        data.lens = 32
    data.clip_start = 0.03
    data.clip_end = 200
    obj = bpy.data.objects.new(name,data)
    COLLECTIONS["90_PREVIEW_ONLY"].objects.link(obj)
    obj.location = bp(local(Vector(position)))
    direction = (bp(local(Vector(target)))-obj.location).normalized()
    right = direction.cross(Vector((0,0,1))).normalized()
    camera_up = right.cross(direction).normalized()
    backward = -direction
    obj.rotation_euler = Matrix(((right.x,camera_up.x,backward.x),
                                 (right.y,camera_up.y,backward.y),
                                 (right.z,camera_up.z,backward.z))).to_euler()
    obj["unity_position"] = list(position)
    obj["unity_target"] = list(target)
    return obj

focus = Vector((-0.05,0.95,0.25))
formal = camera_object("Formal_Unity_Camera",focus+Vector((12,15,-15)),focus,19.0*16/9)
reverse = camera_object("Reverse_From_Goal",(5,1.5,5.2),(-5.9,3.2,-3.65))
scene.camera = formal
world = bpy.data.worlds.new("F_Preview_World")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55,0.68,0.75,1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.65
scene.world = world
sun_data = bpy.data.lights.new("Preview_Sun","SUN")
sun_data.energy = 1.6
sun_data.angle = math.radians(10)
sun = bpy.data.objects.new("Preview_Sun",sun_data)
COLLECTIONS["90_PREVIEW_ONLY"].objects.link(sun)
sun.rotation_euler = (math.radians(28),math.radians(-25),math.radians(-30))
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == "VIEW_3D":
            area.spaces.active.region_3d.view_perspective = "CAMERA"
            area.spaces.active.shading.type = "MATERIAL"
bpy.ops.object.select_all(action="DESELECT")
pavilion_root.select_set(True)
bpy.context.view_layer.objects.active = pavilion_root
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_DIR/"F-GoalTerrace.blend"),check_existing=False)
print("F_GOAL_TERRACE_BUILD_COMPLETE")
print(json.dumps({"total_triangles":manifest["total_triangles"],"alignment":alignment,
                  "pavilion":manifest["pavilion"],"bounds":manifest["bounds_world"]},indent=2))
