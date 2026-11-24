"""Build the D-ElasticPool static basin for Bounce Back.

Run with Blender 5.x from any working directory:

  blender.exe --background --python-exit-code 1 --python build_d_elastic_pool.py

Metres, Unity axes while authoring: X east, Y up, Z north.
The mesh origin is the water-surface centre, world (-1.40, 0.45, 2.05).
No colliders, no water surface, no gameplay objects.
"""

from pathlib import Path
import json
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector

SOURCE_DIR = Path(__file__).resolve().parent
PROJECT_DIR = SOURCE_DIR.parents[2]
DELIVERY_DIR = PROJECT_DIR / "Assets" / "Scenes" / "Archive" / "BounceBack" / "Art" / "D-ElasticPool"
FBX_DIR = DELIVERY_DIR / "FBX"
DOC_DIR = DELIVERY_DIR / "Documentation"
LAYOUT_PATH = PROJECT_DIR / "ArtSource" / "Santorini Layout" / "Santorini Collision Layout.json"
for folder in (FBX_DIR, DOC_DIR, SOURCE_DIR):
    folder.mkdir(parents=True, exist_ok=True)

# Collision truth from SantoriniSystemsAuthoring / the layout JSON.
ANCHOR = (-1.40, 0.45, 2.05)  # water-surface centre, prefab pivot
POOL_SIZE = (3.25, 0.42, 3.65)
X0 = ANCHOR[0] - POOL_SIZE[0] * 0.5  # -3.025
X1 = ANCHOR[0] + POOL_SIZE[0] * 0.5  #  0.225
Z0 = ANCHOR[2] - POOL_SIZE[2] * 0.5  #  0.225
Z1 = ANCHOR[2] + POOL_SIZE[2] * 0.5  #  3.875
WATER_Y = 0.45
COLLISION_BOTTOM_Y = WATER_Y - POOL_SIZE[1]  # 0.03
RIM_TOP_Y = 0.49
RIM_WIDTH = 0.25
FOUNDATION_Y = -0.22
MAX_Y = 0.55
FLOOR_RELIEF_MAX_Y = 0.0495

IVORY, APRICOT, DOME, ROSE = range(4)
MATERIAL_NAMES = ("MV_Ivory", "MV_Apricot", "MV_DomeBlue", "MV_Rose")

# Cross-section, measured outward from the collision side faces.
WALL_OUT = 0.20
COPING_OUT = RIM_WIDTH  # 0.25, top surface
STEP_INNER = 0.198      # 2 mm bite into the wall so the joint is buried
STEP_OUTER = 0.50
STEP_TOP_Y = 0.18
PLINTH_OUT = 0.74
PIER = 0.34
PIER_GAP = 0.001        # keep pier faces off the collision planes


def srgb_to_linear(color):
    def channel(value):
        if value <= 0.04045:
            return value / 12.92
        return ((value + 0.055) / 1.055) ** 2.4

    return tuple(channel(value) for value in color)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"
    collections = {}
    for name in ("01_DELIVERY", "90_PREVIEW"):
        collection = bpy.data.collections.new(name)
        scene.collection.children.link(collection)
        collections[name] = collection
    return scene, collections


def make_material(name, srgb, roughness):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    linear = (*srgb_to_linear(srgb), 1.0)
    material.diffuse_color = linear
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = linear
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = 0.0
    specular = shader.inputs.get("Specular IOR Level") or shader.inputs.get("Specular")
    if specular is not None:
        specular.default_value = 0.28
    return material


def make_outward(vertices, faces):
    centroid = [sum(vertex[axis] for vertex in vertices) / len(vertices) for axis in range(3)]
    fixed = []
    for face in faces:
        a, b, c = (vertices[index] for index in face[:3])
        ux, uy, uz = (b[axis] - a[axis] for axis in range(3))
        vx, vy, vz = (c[axis] - a[axis] for axis in range(3))
        normal = (uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx)
        center = [sum(vertices[index][axis] for index in face) / len(face) for axis in range(3)]
        outward = sum((center[axis] - centroid[axis]) * normal[axis] for axis in range(3))
        fixed.append(tuple(reversed(face)) if outward < 0.0 else tuple(face))
    return fixed


class BasinMesh:
    def __init__(self):
        self.vertices = []
        self.faces = []
        self.materials = []
        self.boxes = []

    def add_box(self, xmin, ymin, zmin, xmax, ymax, zmax, material, name):
        if xmax - xmin < 1e-5 or ymax - ymin < 1e-5 or zmax - zmin < 1e-5:
            raise RuntimeError(f"Degenerate box {name}")
        if ymax > MAX_Y + 1e-6:
            raise RuntimeError(f"{name} rises to {ymax:.4f}, above the {MAX_Y:.2f} limit")
        self._reject_water_column(xmin, ymin, zmin, xmax, ymax, zmax, name)
        corners = [
            (xmin, ymin, zmin), (xmax, ymin, zmin), (xmax, ymax, zmin), (xmin, ymax, zmin),
            (xmin, ymin, zmax), (xmax, ymin, zmax), (xmax, ymax, zmax), (xmin, ymax, zmax),
        ]
        faces = make_outward(corners, (
            (0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4),
            (3, 2, 6, 7), (0, 3, 7, 4), (1, 2, 6, 5),
        ))
        self._append(corners, faces, material)
        self.boxes.append({
            "name": name,
            "min": [xmin, ymin, zmin],
            "max": [xmax, ymax, zmax],
            "material": MATERIAL_NAMES[material],
        })

    def add_diamond(self, center_x, center_z, radius_x, radius_z, ymin, ymax, material, name):
        bottom = [
            (center_x, ymin, center_z + radius_z),
            (center_x + radius_x, ymin, center_z),
            (center_x, ymin, center_z - radius_z),
            (center_x - radius_x, ymin, center_z),
        ]
        top = [(point[0], ymax, point[2]) for point in bottom]
        vertices = bottom + top
        faces = make_outward(vertices, (
            (0, 1, 2, 3),
            (4, 5, 6, 7),
            (0, 4, 5, 1),
            (1, 5, 6, 2),
            (2, 6, 7, 3),
            (3, 7, 4, 0),
        ))
        self._reject_water_column(
            center_x - radius_x, ymin, center_z - radius_z,
            center_x + radius_x, ymax, center_z + radius_z, name)
        self._append(vertices, faces, material)
        self.boxes.append({
            "name": name,
            "min": [center_x - radius_x, ymin, center_z - radius_z],
            "max": [center_x + radius_x, ymax, center_z + radius_z],
            "material": MATERIAL_NAMES[material],
        })

    def add_pyramid(self, center_x, center_z, half, ymin, apex_y, material, name):
        if apex_y > MAX_Y + 1e-6:
            raise RuntimeError(f"{name} rises to {apex_y:.4f}, above the {MAX_Y:.2f} limit")
        vertices = [
            (center_x - half, ymin, center_z - half),
            (center_x + half, ymin, center_z - half),
            (center_x + half, ymin, center_z + half),
            (center_x - half, ymin, center_z + half),
            (center_x, apex_y, center_z),
        ]
        faces = make_outward(vertices, (
            (0, 1, 2, 3),
            (0, 1, 4),
            (1, 2, 4),
            (2, 3, 4),
            (3, 0, 4),
        ))
        self._reject_water_column(
            center_x - half, ymin, center_z - half,
            center_x + half, apex_y, center_z + half, name)
        self._append(vertices, faces, material)

    def _append(self, vertices, faces, material):
        offset = len(self.vertices)
        self.vertices.extend(vertices)
        self.faces.extend(tuple(index + offset for index in face) for face in faces)
        self.materials.extend([material] * len(faces))

    @staticmethod
    def _reject_water_column(xmin, ymin, zmin, xmax, ymax, zmax, name):
        """Nothing may occupy the open water column above the mosaic."""
        if ymax <= FLOOR_RELIEF_MAX_Y + 0.0015:
            return
        inside_x = xmin < X1 - 0.01 and xmax > X0 + 0.01
        inside_z = zmin < Z1 - 0.01 and zmax > Z0 + 0.01
        if inside_x and inside_z:
            raise RuntimeError(f"{name} enters the open water column above the floor")


def add_wrapped_band(mesh, y0, y1, inner, outer, material, name):
    """A rectangular band around the pool, from `inner` to `outer` metres outside the walls."""
    overlap = 0.001
    mesh.add_box(X0 - outer, y0, Z0 - outer, X1 + outer, y1, Z0 - inner, material, name + " South")
    mesh.add_box(X0 - outer, y0, Z1 + inner, X1 + outer, y1, Z1 + outer, material, name + " North")
    mesh.add_box(X0 - outer, y0, Z0 - inner - overlap, X0 - inner, y1, Z1 + inner + overlap, material, name + " West")
    mesh.add_box(X1 + inner, y0, Z0 - inner - overlap, X1 + outer, y1, Z1 + inner + overlap, material, name + " East")


def add_inner_shell(mesh, y0, y1, outer, material, name):
    """Wall or coping whose inner faces lie exactly on the collision sides. Corners are filled."""
    overlap = 0.001
    mesh.add_box(X0 - outer, y0, Z0 - outer, X1 + outer, y1, Z0, material, name + " South")
    mesh.add_box(X0 - outer, y0, Z1, X1 + outer, y1, Z1 + outer, material, name + " North")
    mesh.add_box(X0 - outer, y0, Z0 - overlap, X0, y1, Z1 + overlap, material, name + " West")
    mesh.add_box(X1, y0, Z0 - overlap, X1 + outer, y1, Z1 + overlap, material, name + " East")


def add_corner_piers(mesh):
    corners = (
        ("SW", X0 - PIER, X0 - PIER_GAP, Z0 - PIER, Z0 - PIER_GAP),
        ("SE", X1 + PIER_GAP, X1 + PIER, Z0 - PIER, Z0 - PIER_GAP),
        ("NW", X0 - PIER, X0 - PIER_GAP, Z1 + PIER_GAP, Z1 + PIER),
        ("NE", X1 + PIER_GAP, X1 + PIER, Z1 + PIER_GAP, Z1 + PIER),
    )
    for label, xmin, xmax, zmin, zmax in corners:
        mesh.add_box(xmin, 0.16, zmin, xmax, 0.50, zmax, APRICOT, f"Pier {label}")
        center_x = (xmin + xmax) * 0.5
        center_z = (zmin + zmax) * 0.5
        mesh.add_box(center_x - 0.11, 0.495, center_z - 0.11, center_x + 0.11, 0.524, center_z + 0.11,
                     APRICOT, f"Finial Plinth {label}")
        mesh.add_box(center_x - 0.065, 0.520, center_z - 0.065, center_x + 0.065, 0.540, center_z + 0.065,
                     APRICOT, f"Finial Neck {label}")
        mesh.add_pyramid(center_x, center_z, 0.045, 0.536, MAX_Y, APRICOT, f"Finial Spire {label}")


def add_rim_inlay(mesh):
    """Warm outer edge on the ivory coping. Kept off the corner piers and only 1.5 cm proud."""
    margin = 0.10
    y0, y1 = 0.482, 0.505
    band = 0.08
    mesh.add_box(X0 + margin, y0, Z0 - COPING_OUT, X1 - margin, y1, Z0 - COPING_OUT + band,
                 APRICOT, "Coping Inlay South")
    mesh.add_box(X0 + margin, y0, Z1 + COPING_OUT - band, X1 - margin, y1, Z1 + COPING_OUT,
                 APRICOT, "Coping Inlay North")
    mesh.add_box(X0 - COPING_OUT, y0, Z0 + margin, X0 - COPING_OUT + band, y1, Z1 - margin,
                 APRICOT, "Coping Inlay West")
    mesh.add_box(X1 + COPING_OUT - band, y0, Z0 + margin, X1 + COPING_OUT, y1, Z1 - margin,
                 APRICOT, "Coping Inlay East")


def fit_tiles(length, gap, target):
    best = None
    for count in range(1, 24):
        tile = (length - (count + 1) * gap) / count
        if tile < gap:
            continue
        score = abs(tile - target)
        if best is None or score < best[0]:
            best = (score, count, tile)
    if best is None:
        raise RuntimeError(f"No tile fit for length {length:.3f}")
    return best[1], best[2]


def add_tiled_ring(mesh, x0, x1, z0, z1, band, gap, y0, y1, material, target):
    mesh.add_box(x0, y0, z0, x0 + band, y1, z0 + band, material, "Mosaic Corner SW")
    mesh.add_box(x1 - band, y0, z0, x1, y1, z0 + band, material, "Mosaic Corner SE")
    mesh.add_box(x0, y0, z1 - band, x0 + band, y1, z1, material, "Mosaic Corner NW")
    mesh.add_box(x1 - band, y0, z1 - band, x1, y1, z1, material, "Mosaic Corner NE")

    def run(length, place):
        count, tile = fit_tiles(length, gap, target)
        cursor = gap
        for _ in range(count):
            place(cursor, cursor + tile)
            cursor += tile + gap

    run((x1 - band) - (x0 + band), lambda a, b: mesh.add_box(
        x0 + band + a, y0, z0, x0 + band + b, y1, z0 + band, material, "Mosaic South Tile"))
    run((x1 - band) - (x0 + band), lambda a, b: mesh.add_box(
        x0 + band + a, y0, z1 - band, x0 + band + b, y1, z1, material, "Mosaic North Tile"))
    run((z1 - band) - (z0 + band), lambda a, b: mesh.add_box(
        x0, y0, z0 + band + a, x0 + band, y1, z0 + band + b, material, "Mosaic West Tile"))
    run((z1 - band) - (z0 + band), lambda a, b: mesh.add_box(
        x1 - band, y0, z0 + band + a, x1, y1, z0 + band + b, material, "Mosaic East Tile"))


def add_frame(mesh, x0, x1, z0, z1, band, y0, y1, material, name):
    # West/east bars overlap the north/south bars by 1 mm so the joint faces are buried.
    mesh.add_box(x0, y0, z0, x1, y1, z0 + band, material, name + " South")
    mesh.add_box(x0, y0, z1 - band, x1, y1, z1, material, name + " North")
    mesh.add_box(x0, y0, z0 + band - 0.001, x0 + band, y1, z1 - band + 0.001, material, name + " West")
    mesh.add_box(x1 - band, y0, z0 + band - 0.001, x1, y1, z1 - band + 0.001, material, name + " East")


def build_basin():
    mesh = BasinMesh()
    # Rose plinth, the lower of the two outside steps. One solid course down to the foundation.
    mesh.add_box(X0 - PLINTH_OUT, FOUNDATION_Y, Z0 - PLINTH_OUT,
                 X1 + PLINTH_OUT, -0.02, Z1 + PLINTH_OUT, ROSE, "Rose Plinth")
    # Upper ivory step. Its inner edge tucks 2 mm into the wall; its top is the first tread.
    add_wrapped_band(mesh, -0.03, STEP_TOP_Y, STEP_INNER, STEP_OUTER, IVORY, "Ivory Step")
    # Basin wall, inner face exactly on the collision box, then the wider coping.
    add_inner_shell(mesh, 0.028, 0.40, WALL_OUT, IVORY, "Basin Wall")
    add_inner_shell(mesh, 0.395, RIM_TOP_Y, COPING_OUT, IVORY, "Coping")
    add_rim_inlay(mesh)
    add_corner_piers(mesh)

    # Floor. Grout top is the collision bottom; blue relief stays within 2 cm of it.
    mesh.add_box(X0 - 0.001, 0.012, Z0 - 0.001, X1 + 0.001, COLLISION_BOTTOM_Y, Z1 + 0.001,
                 IVORY, "Floor Grout")
    inset = 0.08
    band = 0.28
    add_tiled_ring(
        mesh, X0 + inset, X1 - inset, Z0 + inset, Z1 - inset,
        band, 0.032, 0.028, 0.045, DOME, 0.42)
    frame_inset = inset + band + 0.06
    frame_band = 0.18
    add_frame(mesh, X0 + frame_inset, X1 - frame_inset, Z0 + frame_inset, Z1 - frame_inset,
              frame_band, 0.028, 0.040, DOME, "Concentric Middle")
    inner_inset = frame_inset + frame_band + 0.06
    inner_band = 0.15
    add_frame(mesh, X0 + inner_inset, X1 - inner_inset, Z0 + inner_inset, Z1 - inner_inset,
              inner_band, 0.028, 0.048, DOME, "Concentric Inner")
    center_inset = inner_inset + inner_band + 0.07
    mesh.add_box(X0 + center_inset, 0.028, Z0 + center_inset, X1 - center_inset, 0.036, Z1 - center_inset,
                 DOME, "Center Field")
    mesh.add_diamond(ANCHOR[0], ANCHOR[2], 0.42, 0.42, 0.034, 0.047, IVORY, "Center Diamond")
    mesh.add_box(ANCHOR[0] - 0.09, 0.044, ANCHOR[2] - 0.09, ANCHOR[0] + 0.09, FLOOR_RELIEF_MAX_Y,
                 ANCHOR[2] + 0.09, DOME, "Center Pupil")
    return mesh


def quat_matrix(x, y, z, w):
    return (
        (1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)),
        (2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)),
        (2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)),
    )


def obb_separated(center_a, extents_a, axes_a, center_b, extents_b, axes_b, slop):
    dots = [[sum(axes_a[row][axis] * axes_b[column][axis] for axis in range(3))
             for column in range(3)] for row in range(3)]
    delta = [center_b[axis] - center_a[axis] for axis in range(3)]
    delta_a = [sum(delta[axis] * axes_a[row][axis] for axis in range(3)) for row in range(3)]
    absolute = [[abs(dots[row][column]) + 1e-6 for column in range(3)] for row in range(3)]
    for row in range(3):
        radius_b = sum(extents_b[column] * absolute[row][column] for column in range(3))
        if abs(delta_a[row]) > extents_a[row] + radius_b + slop:
            return True
    for column in range(3):
        radius_a = sum(extents_a[row] * absolute[row][column] for row in range(3))
        delta_b = sum(delta[axis] * axes_b[column][axis] for axis in range(3))
        if abs(delta_b) > radius_a + extents_b[column] + slop:
            return True
    for row in range(3):
        for column in range(3):
            row_a, row_b = (row + 1) % 3, (row + 2) % 3
            column_a, column_b = (column + 1) % 3, (column + 2) % 3
            radius_a = extents_a[row_a] * absolute[row_b][column] + extents_a[row_b] * absolute[row_a][column]
            radius_b = extents_b[column_a] * absolute[row][column_b] + extents_b[column_b] * absolute[row][column_a]
            projected = abs(delta_a[row_b] * dots[row_a][column] - delta_a[row_a] * dots[row_b][column])
            if projected > radius_a + radius_b + slop:
                return True
    return False


def collision_overlaps(boxes):
    layout = json.loads(LAYOUT_PATH.read_text(encoding="utf-8"))
    identity = ((1, 0, 0), (0, 1, 0), (0, 0, 1))
    hits = []
    for item in layout["items"]:
        if item.get("anchor") or item["name"] == "Elastic Pool Surface":
            continue
        size = item["size"]
        if size["x"] < 1e-4 or size["y"] < 1e-4 or size["z"] < 1e-4:
            continue
        rotation = item["rotation"]
        matrix = quat_matrix(rotation["x"], rotation["y"], rotation["z"], rotation["w"])
        axes = tuple(tuple(matrix[row][column] for row in range(3)) for column in range(3))
        center = (item["center"]["x"], item["center"]["y"], item["center"]["z"])
        extents = (size["x"] * 0.5, size["y"] * 0.5, size["z"] * 0.5)
        for box in boxes:
            box_center = tuple((box["min"][axis] + box["max"][axis]) * 0.5 for axis in range(3))
            box_extents = tuple((box["max"][axis] - box["min"][axis]) * 0.5 for axis in range(3))
            if not obb_separated(box_center, box_extents, identity, center, extents, axes, 0.02):
                hits.append(f"{box['name']} overlaps collider {item['name']}")
    return hits


def measure(mesh):
    xs = [vertex[0] for vertex in mesh.vertices]
    ys = [vertex[1] for vertex in mesh.vertices]
    zs = [vertex[2] for vertex in mesh.vertices]
    interior = [vertex for vertex in mesh.vertices
                if X0 + 0.05 < vertex[0] < X1 - 0.05 and Z0 + 0.05 < vertex[2] < Z1 - 0.05 and vertex[1] < 0.1]

    def extreme(predicate, axis, find_max):
        samples = [vertex[axis] for vertex in mesh.vertices if predicate(vertex)]
        if not samples:
            return None, 0
        return (max(samples) if find_max else min(samples)), len(samples)

    # Corners of each shell sit on the collision plane. The band is only the inner 3 cm,
    # so the extreme toward the pool is the inner face even though a box has no mid-edge vertices.
    def shell_band(vertex):
        return 0.02 < vertex[1] < 0.42

    south, south_count = extreme(
        lambda vertex: shell_band(vertex) and Z0 - 0.03 < vertex[2] <= Z0 + 0.0001, 2, True)
    north, north_count = extreme(
        lambda vertex: shell_band(vertex) and Z1 - 0.0001 <= vertex[2] < Z1 + 0.03, 2, False)
    east, east_count = extreme(
        lambda vertex: shell_band(vertex) and X1 - 0.0001 <= vertex[0] < X1 + 0.03, 0, False)
    west, west_count = extreme(
        lambda vertex: shell_band(vertex) and X0 - 0.03 < vertex[0] <= X0 + 0.0001, 0, True)
    authored = {
        "Basin Wall South": ("max", 2, Z0),
        "Coping South": ("max", 2, Z0),
        "Basin Wall North": ("min", 2, Z1),
        "Coping North": ("min", 2, Z1),
        "Basin Wall East": ("min", 0, X1),
        "Coping East": ("min", 0, X1),
        "Basin Wall West": ("max", 0, X0),
        "Coping West": ("max", 0, X0),
    }
    authored_errors = {}
    for name, (which, axis, expected) in authored.items():
        box = next(item for item in mesh.boxes if item["name"] == name)
        actual = box["max"][axis] if which == "max" else box["min"][axis]
        authored_errors[name] = abs(actual - expected)
    coping_south = next(item for item in mesh.boxes if item["name"] == "Coping South")
    coping_width = abs((coping_south["max"][2] - coping_south["min"][2]) - RIM_WIDTH)
    errors = {
        "south_inner_z": None if south is None else abs(south - Z0),
        "north_inner_z": None if north is None else abs(north - Z1),
        "east_inner_x": None if east is None else abs(east - X1),
        "west_inner_x": None if west is None else abs(west - X0),
        "samples": {
            "south": south_count, "east": east_count, "north": north_count, "west": west_count,
        },
        "authored_shell_m": authored_errors,
        "coping_width_error_m": coping_width,
    }
    # Long beams only store vertices at their ends, so the open north rim is measured from the
    # authored boxes that cross the bounce corridor, not from corner vertices.
    north_rim = [
        box["max"][1] for box in mesh.boxes
        if "North" in box["name"]
        and box["max"][0] > X0 + 0.45 and box["min"][0] < X1 - 0.45
        and box["max"][2] > Z1 - 0.30 and box["max"][1] > 0.2
    ]
    return {
        "bounds_min": [min(xs), min(ys), min(zs)],
        "bounds_max": [max(xs), max(ys), max(zs)],
        "interior_floor_min_y": min(vertex[1] for vertex in interior),
        "interior_floor_max_y": max(vertex[1] for vertex in interior),
        "max_y": max(ys),
        "north_rim_between_piers_max_y": max(north_rim),
        "coping_top_vertex_count": sum(1 for y in ys if abs(y - RIM_TOP_Y) < 0.0015),
        "plane_errors_m": errors,
    }


def assert_ready(mesh, report, overlaps):
    problems = []
    for name, error in report["plane_errors_m"].items():
        if name in ("samples", "authored_shell_m"):
            continue
        if error is None or error > 0.0005:
            problems.append(f"inner plane {name} error is {error}")
    for name, error in report["plane_errors_m"]["authored_shell_m"].items():
        if error > 0.0005:
            problems.append(f"authored shell {name} misses the collision face by {error:.6f} m")
    if abs(report["interior_floor_max_y"] - FLOOR_RELIEF_MAX_Y) > 1e-4:
        problems.append("floor relief does not reach the authored mosaic height")
    grout = [vertex[1] for vertex in mesh.vertices if abs(vertex[1] - COLLISION_BOTTOM_Y) < 1e-4]
    if len(grout) < 4:
        problems.append("collision-bottom grout plane is missing")
    if report["max_y"] > MAX_Y + 1e-6:
        problems.append("module exceeds y 0.55")
    if report["north_rim_between_piers_max_y"] > 0.505 + 1e-4:
        problems.append("north rim between the piers is too tall")
    if report["coping_top_vertex_count"] < 8:
        problems.append("coping top y 0.49 was not found")
    relief_error = report["interior_floor_max_y"] - COLLISION_BOTTOM_Y
    if relief_error > 0.02:
        problems.append(f"floor relief stands {relief_error:.4f} m off the collision bottom")
    hard_overlaps = [hit for hit in overlaps if any(name in hit for name in (
        "Landing Terrace", "Lift Wall", "Pool Drop Ramp", "Cascade Lip Ramp"))]
    problems.extend(hard_overlaps)
    if problems:
        raise RuntimeError(" ; ".join(problems))


def to_blender_mesh(mesh, materials):
    # Unity (x, y, z) -> Blender (x, -z, y). A pure rotation, so winding stays valid.
    converted = [(vertex[0] - ANCHOR[0], -(vertex[2] - ANCHOR[2]), vertex[1] - ANCHOR[1])
                 for vertex in mesh.vertices]
    data = bpy.data.meshes.new("D-ElasticPool_Mesh")
    data.from_pydata(converted, [], mesh.faces)
    data.validate(clean_customdata=False)
    data.update()
    for material in materials:
        data.materials.append(material)
    for polygon, material_index in zip(data.polygons, mesh.materials):
        polygon.material_index = material_index
        polygon.use_smooth = False
    uv = data.uv_layers.new(name="UV0_MetricBox")
    for polygon in data.polygons:
        dominant = max(range(3), key=lambda axis: abs(polygon.normal[axis]))
        axes = ((1, 2), (0, 2), (0, 1))[dominant]
        for loop_index in polygon.loop_indices:
            coordinate = data.vertices[data.loops[loop_index].vertex_index].co
            uv.data[loop_index].uv = (coordinate[axes[0]], coordinate[axes[1]])
    data.update()
    return data


def export_fbx(root, visual):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    visual.select_set(True)
    bpy.context.view_layer.objects.active = visual
    arguments = dict(
        filepath=str(FBX_DIR / "D-ElasticPool.fbx"),
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
    bpy.ops.export_scene.fbx(**{key: value for key, value in arguments.items() if key in supported})


def triangle_count(data):
    return sum(len(polygon.vertices) - 2 for polygon in data.polygons)


def main():
    scene, collections = reset_scene()
    materials = (
        make_material("MV_Ivory", (0.95, 0.88, 0.77), 0.82),
        make_material("MV_Apricot", (0.90, 0.58, 0.37), 0.78),
        make_material("MV_DomeBlue", (0.30, 0.47, 0.80), 0.62),
        make_material("MV_Rose", (0.74, 0.45, 0.47), 0.84),
    )
    basin = build_basin()
    report = measure(basin)
    overlaps = collision_overlaps(basin.boxes)
    assert_ready(basin, report, overlaps)

    data = to_blender_mesh(basin, materials)
    root = bpy.data.objects.new("D-ElasticPool", None)
    collections["01_DELIVERY"].objects.link(root)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.25
    root["pivot_world_m"] = list(ANCHOR)
    root["pivot_definition"] = "Centre of the elastic-pool water surface"
    root["units"] = "metres; Unity X east, Y up, Z north"
    root["visual_only"] = True
    root["contains_colliders"] = False
    root["contains_water_surface"] = False
    visual = bpy.data.objects.new("D-ElasticPool_Mesh", data)
    collections["01_DELIVERY"].objects.link(visual)
    visual.parent = root

    camera_data = bpy.data.cameras.new("Official_Camera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 9.5 * (16.0 / 9.0)
    camera = bpy.data.objects.new("Official_Camera", camera_data)
    collections["90_PREVIEW"].objects.link(camera)
    focus = Vector((-0.05, 0.95, 0.25))
    direction = Vector((12.0, 15.0, -15.0))
    # Preview camera only. Delivery export selects the delivery collection objects explicitly.
    camera.location = (focus.x + direction.x, -(focus.z + direction.z), focus.y + direction.y)
    scene.camera = camera

    triangles = triangle_count(data)
    if triangles > 15000:
        raise RuntimeError(f"Triangle count {triangles} exceeds 15000")
    if len(data.materials) > 4:
        raise RuntimeError("More than 4 material slots")

    blend_path = SOURCE_DIR / "D-ElasticPool.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
    export_fbx(root, visual)

    manifest = {
        "task": "D-ElasticPool",
        "units": "metres",
        "axes": "Unity X east, Y up, Z north",
        "anchor_world": list(ANCHOR),
        "pivot": "Water-surface centre. Prefab root rotation must stay (0,0,0); Blender-to-Unity axis conversion lives on the imported child.",
        "collision": {
            "name": "Elastic Pool Surface",
            "center_world": [ANCHOR[0], COLLISION_BOTTOM_Y + POOL_SIZE[1] * 0.5, ANCHOR[2]],
            "size": list(POOL_SIZE),
            "min_world": [X0, COLLISION_BOTTOM_Y, Z0],
            "max_world": [X1, WATER_Y, Z1],
        },
        "rim": {
            "top_y": RIM_TOP_Y,
            "width_m": RIM_WIDTH,
            "inner_edge": "Exactly the four vertical faces of Elastic Pool Surface",
            "north_edge_max_y_between_piers": report["north_rim_between_piers_max_y"],
            "finial_apex_y": MAX_Y,
        },
        "steps": {
            "upper_tread_y": STEP_TOP_Y,
            "upper_outer_offset_m": STEP_OUTER,
            "lower_tread_y": -0.02,
            "lower_bottom_y": FOUNDATION_Y,
            "lower_outer_offset_m": PLINTH_OUT,
        },
        "floor": {
            "grout_top_y": COLLISION_BOTTOM_Y,
            "relief_max_y": report["interior_floor_max_y"],
            "relief_above_collision_bottom_m": report["interior_floor_max_y"] - COLLISION_BOTTOM_Y,
        },
        "water_surface_for_task_G": {
            "included_in_this_module": False,
            "center_world": list(ANCHOR),
            "y": WATER_Y,
            "size_x": POOL_SIZE[0],
            "size_z": POOL_SIZE[2],
            "min_world": [X0, WATER_Y, Z0],
            "max_world": [X1, WATER_Y, Z1],
            "local_to_prefab": {
                "center": [0.0, 0.0, 0.0],
                "min": [X0 - ANCHOR[0], 0.0, Z0 - ANCHOR[2]],
                "max": [X1 - ANCHOR[0], 0.0, Z1 - ANCHOR[2]],
            },
            "note": "A horizontal rectangle. Its edges meet the vertical inner walls, so the two surfaces share a boundary edge and do not z-fight. Do not raise it above y 0.45.",
        },
        "bounds_world": {"min": report["bounds_min"], "max": report["bounds_max"]},
        "vertices": len(data.vertices),
        "triangles": triangles,
        "material_slots": list(MATERIAL_NAMES),
        "plane_errors_m": report["plane_errors_m"],
        "collider_overlaps_beyond_2cm": overlaps,
        "visual_only": True,
        "source_blend": "ArtSource/Prototypes/D-ElasticPool/D-ElasticPool.blend",
        "rebuild_script": "ArtSource/Prototypes/D-ElasticPool/build_d_elastic_pool.py",
        "fbx": "Assets/Scenes/Archive/BounceBack/Art/D-ElasticPool/FBX/D-ElasticPool.fbx",
    }
    text = json.dumps(manifest, indent=2)
    (SOURCE_DIR / "D-ElasticPool_Dimensions.json").write_text(text, encoding="utf-8")
    (DOC_DIR / "D-ElasticPool_Dimensions.json").write_text(text, encoding="utf-8")
    print("D_ELASTIC_POOL_BUILD_COMPLETE")
    print(text)


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"D_ELASTIC_POOL_BUILD_FAILED: {error}", file=sys.stderr)
        raise
