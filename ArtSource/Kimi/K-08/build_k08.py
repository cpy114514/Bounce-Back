"""Build the original Bounce Back K-08 Aegean arched window.

Run with Blender 5.3:
  blender-launcher.exe --background --factory-startup --python build_k08.py

The single asset contains exactly two watertight mesh parts under one root:
warm white stone and a cobalt inner lining. No glazing, mullions, props,
collision meshes, ground planes, textures, or external dependencies are added.
Geometry is authored in Unity coordinates (X right, Y up, Z forward), then
converted to Blender's Z-up coordinates. FBX uses -Z forward / Y up.
"""
from pathlib import Path
import json
import math
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector

SOURCE_DIR = Path(__file__).resolve().parent
PROJECT_DIR = SOURCE_DIR.parents[2]
DELIVERY_DIR = PROJECT_DIR / "Assets" / "Scenes" / "Archive" / "BounceBack" / "Art" / "K-08"
FBX_DIR = DELIVERY_DIR / "FBX"
PREVIEW_DIR = DELIVERY_DIR / "Preview"
DOC_DIR = DELIVERY_DIR / "Documentation"
for folder in (FBX_DIR, PREVIEW_DIR, DOC_DIR):
    folder.mkdir(parents=True, exist_ok=True)

LOG_FILE = (SOURCE_DIR / "blender-build.log").open("w", encoding="utf-8")
sys.stdout = LOG_FILE
sys.stderr = LOG_FILE

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.name = "K08_Aegean_Arch_Window"
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.unit_settings.length_unit = "METERS"

asset_collection = bpy.data.collections.new("01_ARCH_WINDOW_SINGLE_ASSET")
scene.collection.children.link(asset_collection)
preview_collection = bpy.data.collections.new("90_PREVIEW_CAMERA_LIGHT_ONLY")
scene.collection.children.link(preview_collection)

# Unity (x,y,z) -> Blender (x,-z,y).
C = Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def blender_point(point):
    return C @ Vector((*point, 1.0))


def unity_point(point):
    result = C.inverted() @ Vector((*point, 1.0))
    return result.to_3d()


def make_material(name, color, roughness):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = (*color, 1.0)
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = (*color, 1.0)
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Metallic"].default_value = 0.0
    return material


WHITE = make_material("K08_White_Limestone", (0.91, 0.885, 0.80), 0.82)
COBALT = make_material("K08_Cobalt_Blue", (0.025, 0.105, 0.52), 0.56)

WIDTH = 1.20
HEIGHT = 1.80
DEPTH = 0.24
ARCH_SEGMENTS = 24
OUTER_RADIUS = WIDTH * 0.5
OUTER_SPRING = HEIGHT - OUTER_RADIUS
STONE_INNER_RADIUS = 0.44
STONE_INNER_BOTTOM = 0.14
LINER_THICKNESS = 0.032
LINER_DEPTH = 0.20
CLEAR_RADIUS = STONE_INNER_RADIUS - LINER_THICKNESS
CLEAR_BOTTOM = STONE_INNER_BOTTOM + LINER_THICKNESS
SPRING = OUTER_SPRING


def arch_profile(radius, bottom):
    """CCW flat-bottomed arched loop in Unity XY, with matching point counts."""
    points = [(-radius, bottom), (radius, bottom)]
    for index in range(ARCH_SEGMENTS + 1):
        angle = math.pi * index / ARCH_SEGMENTS
        points.append((radius * math.cos(angle), SPRING + radius * math.sin(angle)))
    return points


def create_ring(name, outer_profile, inner_profile, depth, material, bevel_width):
    """Create a closed ring prism with a genuine through-hole, not a boolean decal."""
    count = len(outer_profile)
    assert count == len(inner_profile)
    back = -depth * 0.5
    front = depth * 0.5
    vertices = []
    for profile, z in ((outer_profile, back), (outer_profile, front),
                       (inner_profile, back), (inner_profile, front)):
        vertices.extend(blender_point((x, y, z)).to_3d() for x, y in profile)

    ob, of, ib, inf = 0, count, count * 2, count * 3
    faces = []
    for index in range(count):
        nxt = (index + 1) % count
        # Front and rear annuli.
        faces.append((of + index, of + nxt, inf + nxt, inf + index))
        faces.append((ib + index, ib + nxt, ob + nxt, ob + index))
        # Exterior and opening walls. Their normals point away from the solid.
        faces.append((ob + index, ob + nxt, of + nxt, of + index))
        faces.append((ib + index, inf + index, inf + nxt, ib + nxt))

    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    for edge in bm.edges:
        if edge.is_manifold and edge.calc_face_angle() > math.radians(60):
            edge.smooth = False
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(material)
    for polygon in mesh.polygons:
        polygon.material_index = 0
        polygon.use_smooth = True

    uv_layer = mesh.uv_layers.new(name="UV0_Metric")
    for polygon in mesh.polygons:
        dominant = max(range(3), key=lambda axis: abs(polygon.normal[axis]))
        axes = ((1, 2), (0, 2), (0, 1))[dominant]
        for loop_index in polygon.loop_indices:
            vertex = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            uv_layer.data[loop_index].uv = (vertex[axes[0]], vertex[axes[1]])

    obj = bpy.data.objects.new(name, mesh)
    asset_collection.objects.link(obj)
    obj.parent = asset_root
    bevel = obj.modifiers.new("Fine_Edge_Chamfer", "BEVEL")
    bevel.width = bevel_width
    bevel.segments = 2
    bevel.limit_method = "ANGLE"
    bevel.angle_limit = math.radians(30)
    bevel.harden_normals = True
    bevel.use_clamp_overlap = True
    weighted = obj.modifiers.new("Area_Weighted_Normals", "WEIGHTED_NORMAL")
    weighted.keep_sharp = True
    weighted.weight = 50
    return obj


asset_root = bpy.data.objects.new("K08_Aegean_Arch_Window", None)
asset_collection.objects.link(asset_root)
asset_root.empty_display_type = "PLAIN_AXES"
asset_root.empty_display_size = 0.16
asset_root["task"] = "K-08"
asset_root["design"] = "Original Aegean white-stone arch with cobalt inner lining"
asset_root["units"] = "meters; Unity +Y up, +Z forward; front is +Z"
asset_root["pivot"] = "Bottom bounds center, local (0,0,0)"
asset_root["overall_dimensions_m"] = [WIDTH, HEIGHT, DEPTH]
asset_root["visual_only"] = True
asset_root["contains_glazing"] = False

stone = create_ring(
    "K08_White_Stone_Frame",
    arch_profile(OUTER_RADIUS, 0.0),
    arch_profile(STONE_INNER_RADIUS, STONE_INNER_BOTTOM),
    DEPTH,
    WHITE,
    0.008,
)
stone["part_role"] = "White stone structural border; 8 mm fine chamfer"
liner = create_ring(
    "K08_Cobalt_Inner_Liner",
    arch_profile(STONE_INNER_RADIUS, STONE_INNER_BOTTOM),
    arch_profile(CLEAR_RADIUS, CLEAR_BOTTOM),
    LINER_DEPTH,
    COBALT,
    0.003,
)
liner["part_role"] = "Independent cobalt sleeve; 3 mm fine chamfer; 20 cm deep"
liner["lining_thickness_m"] = LINER_THICKNESS

bpy.context.view_layer.update()
depsgraph = bpy.context.evaluated_depsgraph_get()


def evaluated_mesh(obj):
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    return evaluated, mesh


def topology_report(obj):
    evaluated, mesh = evaluated_mesh(obj)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.normal_update()
    report = {
        "vertices": len(bm.verts),
        "edges": len(bm.edges),
        "faces": len(bm.faces),
        "triangles": sum(len(face.verts) - 2 for face in bm.faces),
        "non_manifold_edges": sum(not edge.is_manifold for edge in bm.edges),
        "wire_edges": sum(edge.is_wire for edge in bm.edges),
        "zero_area_faces": sum(face.calc_area() < 1e-10 for face in bm.faces),
        "signed_volume_m3": bm.calc_volume(signed=True),
    }
    bm.free()
    evaluated.to_mesh_clear()
    return report


def local_bounds_unity(obj):
    evaluated, mesh = evaluated_mesh(obj)
    points = [unity_point(vertex.co) for vertex in mesh.vertices]
    minimum = [min(point[axis] for point in points) for axis in range(3)]
    maximum = [max(point[axis] for point in points) for axis in range(3)]
    evaluated.to_mesh_clear()
    return minimum, maximum


def ray_hits(x, height):
    origin = Vector((x, -1.0, height))
    direction = Vector((0.0, 1.0, 0.0))
    result = scene.ray_cast(depsgraph, origin, direction, distance=2.0)
    return bool(result[0])


mesh_objects = [obj for obj in asset_collection.objects if obj.type == "MESH"]
assert len(mesh_objects) == 2, "The asset must contain exactly two mesh parts."
assert asset_root.location.length < 1e-8
assert stone.parent == asset_root and liner.parent == asset_root

for obj, expected_depth in ((stone, DEPTH), (liner, LINER_DEPTH)):
    minimum, maximum = local_bounds_unity(obj)
    dimensions = [maximum[i] - minimum[i] for i in range(3)]
    expected = [WIDTH if obj == stone else STONE_INNER_RADIUS * 2,
                HEIGHT if obj == stone else SPRING + STONE_INNER_RADIUS - STONE_INNER_BOTTOM,
                expected_depth]
    for actual, target in zip(dimensions, expected):
        assert abs(actual - target) < 1e-5, (obj.name, dimensions, expected)

stone_min, stone_max = local_bounds_unity(stone)
for actual, target in zip(stone_min, (-WIDTH * 0.5, 0.0, -DEPTH * 0.5)):
    assert abs(actual - target) < 1e-5, (stone_min, stone_max)
for actual, target in zip(stone_max, (WIDTH * 0.5, HEIGHT, DEPTH * 0.5)):
    assert abs(actual - target) < 1e-5, (stone_min, stone_max)

clear_samples = {
    "center": (0.0, 0.90),
    "upper_arch": (0.0, 1.56),
    "left_jamb_clear": (-0.36, 1.0),
    "right_jamb_clear": (0.36, 1.0),
    "lower_clear": (0.0, 0.22),
}
white_samples = {
    "left_outer_jamb": (-0.55, 0.90),
    "right_outer_jamb": (0.55, 0.90),
    "white_crown": (0.0, 1.72),
    "white_base": (0.0, 0.07),
}
blue_samples = {
    "left_blue_reveal": (-0.425, 0.90),
    "right_blue_reveal": (0.425, 0.90),
    "blue_crown": (0.0, 1.625),
    "blue_base": (0.0, 0.155),
}
for name, (x, height) in clear_samples.items():
    assert not ray_hits(x, height), "Blocked clear opening sample: " + name
for name, (x, height) in {**white_samples, **blue_samples}.items():
    assert ray_hits(x, height), "Missing solid sample: " + name

# Export only the single root and its two mesh children.
bpy.ops.object.select_all(action="DESELECT")
asset_root.select_set(True)
stone.select_set(True)
liner.select_set(True)
bpy.context.view_layer.objects.active = stone
fbx_path = FBX_DIR / "K08_Aegean_Arch_Window.fbx"
kwargs = dict(
    filepath=str(fbx_path),
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

# Preview helpers are cameras/lights only; no additional mesh is created.
def make_camera(name, position, target, ortho_width):
    data = bpy.data.cameras.new(name)
    data.type = "ORTHO"
    data.sensor_fit = "HORIZONTAL"
    data.ortho_scale = ortho_width
    data.clip_start = 0.05
    data.clip_end = 100
    obj = bpy.data.objects.new(name, data)
    preview_collection.objects.link(obj)
    obj.location = blender_point(position).to_3d()
    direction = (blender_point(target).to_3d() - obj.location).normalized()
    right = direction.cross(Vector((0, 0, 1))).normalized()
    up = right.cross(direction).normalized()
    backward = -direction
    orientation = Matrix(((right.x, up.x, backward.x),
                          (right.y, up.y, backward.y),
                          (right.z, up.z, backward.z)))
    obj.rotation_euler = orientation.to_euler()
    return obj


def make_area(name, position, target, energy, size):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    preview_collection.objects.link(obj)
    obj.location = blender_point(position).to_3d()
    direction = blender_point(target).to_3d() - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    return obj


hero_camera = make_camera("K08_Hero_Camera", (2.7, 2.15, 4.8), (0, 0.90, 0), 2.12)
front_camera = make_camera("K08_Front_Camera", (0, 0.90, 5.0), (0, 0.90, 0), 1.65)
world = bpy.data.worlds.new("K08_Aegean_Sky")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.66, 0.79, 0.88, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.65
scene.world = world
sun_data = bpy.data.lights.new("K08_Sun", "SUN")
sun_data.energy = 2.0
sun_data.angle = math.radians(10)
sun = bpy.data.objects.new("K08_Sun", sun_data)
preview_collection.objects.link(sun)
sun.rotation_euler = (math.radians(28), math.radians(-24), math.radians(-28))
make_area("K08_Soft_Key", (2.5, 4.0, 3.5), (0, 0.9, 0), 420, 3.0)
make_area("K08_Soft_Fill", (-3.0, 2.0, 1.5), (0, 0.9, 0), 160, 2.5)

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
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.filepath = str(PREVIEW_DIR / filename)
    bpy.ops.render.render(write_still=True)


render(hero_camera, "K08_Aegean_Arch_Window_Preview.png", 1200, 1400)
render(front_camera, "K08_Aegean_Arch_Window_Front.png", 1000, 1200)
scene.camera = hero_camera
scene.render.resolution_x = 1200
scene.render.resolution_y = 1400
bpy.ops.object.select_all(action="DESELECT")
asset_root.select_set(True)
bpy.context.view_layer.objects.active = asset_root
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == "VIEW_3D":
            area.spaces.active.region_3d.view_perspective = "CAMERA"
            area.spaces.active.shading.type = "MATERIAL"

blend_path = SOURCE_DIR / "K08_Aegean_Arch_Window.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)

reports = {obj.name: topology_report(obj) for obj in mesh_objects}
for name, report in reports.items():
    assert report["non_manifold_edges"] == 0, (name, report)
    assert report["wire_edges"] == 0, (name, report)
    assert report["zero_area_faces"] == 0, (name, report)
    assert report["signed_volume_m3"] > 0, (name, report)

manifest = {
    "task": "K-08",
    "asset": "K08_Aegean_Arch_Window",
    "source_blend": "ArtSource/Kimi/K-08/K08_Aegean_Arch_Window.blend",
    "rebuild_script": "ArtSource/Kimi/K-08/build_k08.py",
    "fbx": "Assets/Scenes/Archive/BounceBack/Art/K-08/FBX/K08_Aegean_Arch_Window.fbx",
    "units": "meters",
    "unity_axes": "+Y up, +Z forward; visible front is +Z",
    "pivot": {"unity_local": [0, 0, 0], "meaning": "bottom bounds center"},
    "overall_dimensions_m": {"width": WIDTH, "height": HEIGHT, "depth": DEPTH},
    "stone_bounds_m": {"min": stone_min, "max": stone_max},
    "outer_arch": {"radius_m": OUTER_RADIUS, "spring_height_m": SPRING, "segments": ARCH_SEGMENTS},
    "stone_border_m": {"side_and_crown": OUTER_RADIUS - STONE_INNER_RADIUS, "base": STONE_INNER_BOTTOM},
    "cobalt_liner": {"thickness_m": LINER_THICKNESS, "depth_m": LINER_DEPTH, "front_back_white_reveal_m": (DEPTH - LINER_DEPTH) * 0.5},
    "clear_opening_m": {
        "width": CLEAR_RADIUS * 2,
        "height": SPRING + CLEAR_RADIUS - CLEAR_BOTTOM,
        "bottom": CLEAR_BOTTOM,
        "arch_crown": SPRING + CLEAR_RADIUS,
        "spring_height": SPRING,
    },
    "chamfers_m": {"white_stone": 0.008, "cobalt_liner": 0.003, "segments": 2},
    "materials": [
        {"name": WHITE.name, "base_color_linear": list(WHITE.diffuse_color), "roughness": 0.82},
        {"name": COBALT.name, "base_color_linear": list(COBALT.diffuse_color), "roughness": 0.56},
    ],
    "external_textures": [],
    "mesh_part_count": len(mesh_objects),
    "contains_glazing": False,
    "contains_colliders": False,
    "topology": reports,
    "through_opening_ray_tests": {
        "clear_samples_passed": list(clear_samples),
        "white_solid_samples_passed": list(white_samples),
        "blue_solid_samples_passed": list(blue_samples),
    },
    "previews": [
        "Preview/K08_Aegean_Arch_Window_Preview.png",
        "Preview/K08_Aegean_Arch_Window_Front.png",
    ],
    "fbx_bytes": fbx_path.stat().st_size,
}
(DOC_DIR / "K08_Dimensions.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
print("K08_BUILD_COMPLETE", fbx_path)
print(json.dumps({"dimensions_m": [WIDTH, HEIGHT, DEPTH], "clear_opening_m": manifest["clear_opening_m"], "topology": reports}, indent=2))
LOG_FILE.close()
