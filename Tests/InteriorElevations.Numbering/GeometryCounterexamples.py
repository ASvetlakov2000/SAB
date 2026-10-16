"""Arithmetic counterexamples from existing SAB formulas; does not execute Revit."""
import math

checks = 0


def check(value, title):
    global checks
    assert value, title
    checks += 1
    print("PASS", title)


old_min, old_max = sorted((3000 - 50, 0 + 100))
expected_min, expected_max = min(3000, 0) - 50, max(3000, 0) + 100
check(old_max - old_min == 2850, "Existing reversed crop formula gives2850mm")
check(expected_max - expected_min == 3150, "Sort endpoints before margins gives3150mm")
old_min, old_max = sorted((3000 - 50, 0 + 50))
check(old_max - old_min == 2900, "Equal margins also fail with reversed endpoints")
check(math.cos(math.radians(10)) < 0.99, "10degree residual enters existing PI correction")
check(math.cos(math.radians(10 + 180)) < -0.98, "PI correction converts10degree residual to opposite side")

old_world_x = (-1, 3)
copied_local_x = (-1, 3)
new_world_x = tuple(sorted(-x for x in copied_local_x))
check(new_world_x == (-3, 1) and new_world_x != old_world_x, "Copying local crop coordinates after180 changes physical extents")
correct_new_local = tuple(sorted(-x for x in old_world_x))
check(tuple(sorted(-x for x in correct_new_local)) == old_world_x, "Reprojected crop corners preserve physical extents")


def inside_l_room(x, y, z=1):
    return 0 < z < 3 and ((0 < x < 4 and 0 < y < 1) or (0 < x < 1 and 0 < y < 4))


check(not inside_l_room(1.2 - .15, 2.5) and not inside_l_room(1.2 + .15, 2.5), "400mm wall can make both150mm probes outside")
check(3 - 1.2 > 0 and inside_l_room(.9, 2.5), "Room Location fallback points right despite room interior on left")
check(inside_l_room(1.05 - .15, 2.5) and not inside_l_room(1.05 + .15, 2.5), "100mm wall alone yields correct selected room side")
check(not inside_l_room(.9, 2.5, -1), "Probe Z outside room volume invalidates horizontal side test")
check(not inside_l_room(.2 + 1, 2.5), "Large marker offset can leave selected room")
print(f"COMPLETED {checks} arithmetic checks; existing formulas, not Revit reproduction")
