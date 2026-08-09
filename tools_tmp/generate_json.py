#!/usr/bin/env python3
"""Generate a JSON drawing plan for the 4 Lusaran as-built sheets.

The JSON is consumed by render_json.ps1, which creates entities directly via
the AutoCAD COM API (no command prompts / script queue issues).

All coordinates are paper-space METERS (drawing is metric, INSUNITS=6;
20"x30" sheet = 0.762 x 0.508 m). Reconstruction per docs/drawing.md.
"""
import json
import sys

W, H = 0.762, 0.508
MX, MY = 0.008, 0.008
TS = 0.062
COL_R = W - MX
COL_L = COL_R - 0.116
NOTE_X = 0.012
NOTE_Y = MY + TS + 0.008

TB = "LUSARAN_TB"
NOTE = "LUSARAN_NOTE"
DASH = "LUSARAN_DASH"


def line(E, x1, y1, x2, y2):
    E.append({"t": "line", "x1": x1, "y1": y1, "x2": x2, "y2": y2})


def rect(E, x1, y1, x2, y2):
    E.append({"t": "rect", "x1": x1, "y1": y1, "x2": x2, "y2": y2})


def pline(E, pts, closed=False):
    E.append({"t": "pline", "pts": pts, "closed": closed})


def text(E, x, y, h, s, rot=0.0):
    E.append({"t": "text", "x": x, "y": y, "h": h, "s": s, "rot": rot})


def circle(E, cx, cy, r):
    E.append({"t": "circle", "cx": cx, "cy": cy, "r": r})


def layer_set(E, name):
    E.append({"t": "layer", "name": name})


def layer_def(E, name, color=None, ltype=None):
    E.append({"t": "layerdef", "name": name, "color": color, "ltype": ltype})


def ltscale(E, v):
    E.append({"t": "ltscale", "v": v})


class V:
    def __init__(self, mmpp, ox, oy):
        self.mmpp = mmpp
        self.ox = ox
        self.oy = oy

    def P(self, x, y):
        return (x / self.mmpp + self.ox, y / self.mmpp + self.oy)

    def L(self, E, x1, y1, x2, y2):
        px1, py1 = self.P(x1, y1)
        px2, py2 = self.P(x2, y2)
        line(E, px1, py1, px2, py2)

    def R(self, E, x1, y1, x2, y2):
        px1, py1 = self.P(x1, y1)
        px2, py2 = self.P(x2, y2)
        rect(E, px1, py1, px2, py2)

    def C(self, E, cx, cy, r_mm):
        px, py = self.P(cx, cy)
        circle(E, px, py, r_mm / self.mmpp)

    def T(self, E, x, y, h_m, txt, rot=0.0):
        px, py = self.P(x, y)
        text(E, px, py, h_m, txt, rot)

    def DIMH(self, E, x1, x2, y_mm, label, eymm):
        px1, py1 = self.P(x1, eymm)
        px2, py2 = self.P(x2, eymm)
        p1, p2 = self.P(x1, y_mm), self.P(x2, y_mm)
        line(E, px1, py1, px1, p1[1])
        line(E, px2, py2, px2, p2[1])
        line(E, p1[0], p1[1], p2[0], p2[1])
        t = 0.0035
        line(E, p1[0] - t, p1[1] - t, p1[0] + t, p1[1] + t)
        line(E, p2[0] - t, p2[1] + t, p2[0] + t, p2[1] - t)
        cx = (p1[0] + p2[0]) / 2
        text(E, cx, p1[1] + 0.002, 0.0018, label)

    def DIMV(self, E, y1, y2, x_mm, label, exmm):
        px1, py1 = self.P(exmm, y1)
        px2, py2 = self.P(exmm, y2)
        p1, p2 = self.P(x_mm, y1), self.P(x_mm, y2)
        line(E, px1, py1, p1[0], py1)
        line(E, px2, py2, p2[0], py2)
        line(E, p1[0], p1[1], p2[0], p2[1])
        t = 0.0035
        line(E, p1[0] - t, p1[1] - t, p1[0] + t, p1[1] + t)
        line(E, p2[0] - t, p2[1] + t, p2[0] + t, p2[1] - t)
        cy = (p1[1] + p2[1]) / 2
        text(E, p1[0] + 0.002, cy, 0.0018, label, 90.0)


def setup(E):
    ltscale(E, 0.003)
    layer_def(E, TB, color=7)
    layer_def(E, NOTE, color=1)
    layer_def(E, DASH, color=1, ltype="DASHED")


def north_arrow(E, cx, cy, r=0.012):
    circle(E, cx, cy, r)
    line(E, cx, cy - r * 0.8, cx, cy + r * 0.8)
    pline(E, [(cx - 0.004, cy - 0.004), (cx, cy + r * 0.6), (cx + 0.004, cy - 0.004)], closed=True)
    text(E, cx - 0.002, cy + r + 0.003, 0.0025, "N")


def grid_bubble(E, cx, cy, letter, r=0.007):
    circle(E, cx, cy, r)
    text(E, cx - 0.0022, cy - 0.0015, 0.0022, letter)


def section_callout(E, cx, cy, letter, leader_len=0.02):
    circle(E, cx, cy, 0.0065)
    text(E, cx - 0.0022, cy - 0.0015, 0.0022, letter)
    line(E, cx, cy - 0.0065, cx, cy - 0.0065 - leader_len)
    t = 0.003
    line(E, cx - t, cy - 0.0065 - leader_len + t, cx + t, cy - 0.0065 - leader_len - t)


def title_block(E, sheet_no, view_names, content_label="WATER TREATMENT PLANT 1"):
    layer_set(E, TB)
    rect(E, 0, 0, W, H)
    rect(E, MX, MY, W - MX, H - MY)
    y0, y1 = MY, MY + TS
    line(E, MX, y1, W - MX, y1)
    colw = [0.086, 0.086, 0.086, 0.076, 0.096, 0.076, 0.090, 0.076, 0.074]
    labs = ["OWNER", "DESIGNER", "ARCHITECT", "VALIDATED BY", "NOTE", "APPROVAL",
            "PROJECT TITLE", "DRAWING CONTENT", "DRAWING NUMBER"]
    x = MX
    for cw, lab in zip(colw, labs):
        line(E, x, y0, x, y1)
        text(E, x + 0.003, y1 - 0.006, 0.0017, lab)
        x += cw
    line(E, x, y0, x, y1)

    def cell_text(col_start, lines, hs=0.0018):
        yy = y1 - 0.011
        for ln in lines:
            text(E, col_start + 0.003, yy, hs, ln)
            yy -= hs + 0.0008

    x = MX
    cell_text(x, ["JE HYDRO & BIO", "ENERGY, CORP.", "CAGAYAN DE ORO CITY"]); x += colw[0]
    cell_text(x, ["WORLD'S FIRST", "WATER VENTURES, INC.", "CAGAYAN DE ORO CITY"]); x += colw[1]
    cell_text(x, ["RYANNEL S. BULAC", "ARCHITECT", "PRC No. 26481"]); x += colw[2]
    cell_text(x, ["ENGR. MARY HAZEL C.", "ABASOLO", "PRESIDENT/COO, WFWVI"]); x += colw[3]
    cell_text(x, ["THIS DRAWING AS AN INSTRUMENT OF SERVICE IS THE PROPERTY OF JE HYDRO & BIO-ENERGY",
                  "CORPORATION, AND SHOULD NOT BE REPRODUCED IN PART OR WHOLE WITHOUT WRITTEN",
                  "PERMISSION."], hs=0.0015); x += colw[4]
    cell_text(x, ["ENGR. JOFFREY E.", "HAPITAN", "CHAIRMAN, JEHBEC"]); x += colw[5]
    cell_text(x, ["MCWD LAHUG BULK", "WATER SUPPLY PROJECT", "LOCATION: CEBU CITY"]); x += colw[6]
    cell_text(x, [content_label]); x += colw[7]
    cell_text(x, ["A-WTP-" + str(sheet_no)] + view_names + ["REV 0", "SIZE 20x30"], hs=0.0015)

    rx0 = COL_L
    line(E, rx0, y0, rx0, H - MY)
    rect(E, rx0, 0.458, COL_R, 0.500)
    text(E, (rx0 + COL_R) / 2 - 0.012, 0.465, 0.0024, "AS-BUILT")
    text(E, (rx0 + COL_R) / 2 - 0.012, 0.479, 0.0024, "PHASE")
    rect(E, rx0, 0.300, COL_R, 0.458)
    text(E, (rx0 + COL_R) / 2 - 0.010, 0.443, 0.0018, "KEY PLAN")
    kp = ["CAB", "ISP", "RW", "CD", "FB", "SB", "RSF", "AB", "MCC", "BP1", "SP1", "SP2", "SP3", "LAGOON"]
    bw = (COL_R - rx0 - 0.012) / 7.0
    for i, nm in enumerate(kp):
        row = i // 7
        col = i % 7
        bx = rx0 + 0.006 + col * bw
        by = 0.318 - row * 0.040
        rect(E, bx, by, bx + bw - 0.003, by + 0.014)
        text(E, bx + 0.0015, by + 0.003, 0.0014, nm)
    north_arrow(E, COL_R - 0.02, 0.412, r=0.010)
    rect(E, rx0, 0.200, COL_R, 0.300)
    text(E, rx0 + 0.004, 0.291, 0.0016, "REV")
    text(E, rx0 + 0.045, 0.291, 0.0016, "DESCRIPTION")
    text(E, rx0 + 0.085, 0.291, 0.0016, "DATE")
    line(E, rx0, 0.283, COL_R, 0.283)
    text(E, rx0 + 0.004, 0.270, 0.0015, "0")
    text(E, rx0 + 0.045, 0.270, 0.0015, "AS-BUILT DRAWING")
    text(E, rx0 + 0.085, 0.270, 0.0015, "09 DEC 2022")
    text(E, rx0 + 0.004, 0.252, 0.0014, "DRN RLF/JRB")
    text(E, rx0 + 0.004, 0.240, 0.0014, "CHK WMD")


def reconstruction_note(E, scale_note):
    layer_set(E, NOTE)
    text(E, NOTE_X, NOTE_Y, 0.0017,
         "RECONSTRUCTION NOTE: " + scale_note +
         " - APPROXIMATION - VERIFY AGAINST SOURCE SHEETS (SPEC ITEMS 1-7)")


def sheet1():
    E = []
    setup(E)
    title_block(E, 1, ["PLAN", "EL. +149.7m"], "CHEMICAL DOSING / RW / FB / SB PLAN")
    v = V(142000, 0.015, 0.150)
    layer_set(E, "A-WTP-1_GEOM")

    v.R(E, 0, 0, 6000, 15400)
    v.L(E, 300, 0, 300, 15400)
    v.L(E, 5600, 0, 5600, 15400)
    v.L(E, 0, 3075, 6000, 3075)
    v.L(E, 0, 8575, 6000, 8575)
    v.T(E, 1500, 11600, 0.0022, "CHEMICAL DOSING")
    v.T(E, 1500, 11100, 0.0018, "F.L. +149.7m")

    v.R(E, 6000, 0, 9000, 15400)
    v.T(E, 6300, 11600, 0.0022, "STOCK ROOM")
    v.T(E, 6300, 11100, 0.0016, "(DIMS UNKNOWN)")

    v.R(E, 9000, 0, 11600, 15400)
    v.T(E, 9300, 11600, 0.0022, "RECEIVING WELL")

    fx0, fx1 = 11600, 11600 + 4700 + 200 + 4700
    fy1 = 4 * 4700 + 3 * 200
    v.R(E, fx0, 0, fx1, fy1)
    v.L(E, fx0 + 4700, 0, fx0 + 4700, fy1)
    v.L(E, fx0 + 4900, 0, fx0 + 4900, fy1)
    for r in range(1, 4):
        v.L(E, fx0, r * 4700 + (r - 1) * 200, fx1, r * 4700 + (r - 1) * 200)
    v.T(E, fx0 + 300, fy1 - 2600, 0.0022, "FLOCCULATION BASIN")
    v.T(E, fx0 + 300, fy1 - 3100, 0.0018, "8 CELLS (4 ROWS x 2 STAGES)")

    sx0 = fx1
    sx1 = sx0 + 59500
    sy1 = 1250 + 4700 + 4700 + 4700 + 4700 + 1250
    v.R(E, sx0, 0, sx1, sy1)
    for yy in (1250, 1250 + 4700, 1250 + 2 * 4700, 1250 + 3 * 4700, 1250 + 4 * 4700):
        v.L(E, sx0, yy, sx1, yy)
    v.T(E, sx0 + 400, sy1 - 3000, 0.0024, "SEDIMENTATION BASIN")
    v.T(E, sx0 + 400, sy1 - 3600, 0.0018, "OVERALL 59500 (see note: 58750 on WTP-2/3/4)")

    v.L(E, 0, sy1, sx1 + 600, sy1)
    v.L(E, 0, sy1 + 600, sx1 + 600, sy1 + 600)
    v.L(E, 0, -600, sx1 + 600, -600)
    v.L(E, 0, 0, sx1 + 600, 0)
    v.L(E, sx1, -600, sx1, sy1 + 600)
    v.L(E, sx1 + 600, -600, sx1 + 600, sy1 + 600)
    v.T(E, sx1 - 18000, sy1 + 950, 0.0018, "OPEN CANAL W=600 H=650")
    v.T(E, sx1 - 14000, sy1 + 150, 0.0018, "TO NEAREST SLUDGE")

    layer_set(E, DASH)
    v.R(E, -400, -1000, sx1 + 1000, sy1 + 1000)
    v.T(E, -400, -1900, 0.0018, "FOUNDATION LINE")
    layer_set(E, "A-WTP-1_GEOM")

    v.L(E, 0, 0, sx1, 0)
    v.L(E, 0, sy1, sx1, sy1)
    for gx in (0, 9000, fx1, sx1):
        v.L(E, gx, 0, gx, sy1)
    gy_top = sy1 + 600 + 1600
    gy_bot = -600 - 1600
    for letter, gx in (("1", 0), ("2", 9000), ("3", fx1), ("4", sx1)):
        grid_bubble(E, *v.P(gx, gy_top), letter)
        grid_bubble(E, *v.P(gx, gy_bot), letter)

    section_callout(E, *v.P(3000, sy1 + 2600), "A", 0.02)
    section_callout(E, *v.P(fx0 + 3000, sy1 + 2600), "B", 0.02)
    section_callout(E, *v.P(sx0 + 20000, sy1 + 2600), "C", 0.02)

    dim_y = -2400
    v.DIMH(E, 0, 6000, dim_y, "6000", 0)
    v.DIMH(E, 6000, 9000, dim_y, "3000", 0)
    v.DIMH(E, 9000, 11600, dim_y, "2600", 0)
    v.DIMH(E, 11600, fx1, dim_y, "9600", 0)
    v.DIMH(E, fx1, sx1, dim_y, "59500", 0)
    v.DIMH(E, 0, sx1, dim_y - 900, "OVERALL 80700", 0)
    v.DIMV(E, 0, 15400, -2600, "15400", 0)
    v.DIMV(E, 0, fy1, fx1 + 2600, "19400", fx1)
    v.DIMV(E, 0, sy1, sx1 + 2600, "20750", sx1)

    north_arrow(E, *v.P(sx1 - 9000, sy1 + 4200), r=0.010)

    text(E, 0.015, 0.472, 0.0030, "A-WTP-1  -  PLAN @ EL. +149.7m")
    text(E, 0.015, 0.460, 0.0020,
         "CHEMICAL DOSING PLAN / RECEIVING WELL PLAN / FLOCCULATION BASIN PLAN / SEDIMENTATION BASIN PLAN")
    reconstruction_note(E, "SCALE APPROX 1:142 (SPEC ITEM 1); CD DEPTH SPLITS SUM 17150 vs 15400 DRAWN")
    return E


def sheet2():
    E = []
    setup(E)
    title_block(E, 2, ["PLAN", "EL. +151.0m"], "CHEMICAL DOSING / RW / FB / SB PLAN (UPPER)")
    v = V(142000, 0.015, 0.150)
    layer_set(E, "A-WTP-2_GEOM")

    v.R(E, 0, 0, 6075, 18625)
    v.L(E, 300, 0, 300, 18625)
    v.L(E, 5000, 0, 5000, 18625)
    for yy in (2750, 3125, 3425, 9325, 14525, 16075, 17075):
        v.L(E, 0, yy, 6075, yy)
    v.T(E, 400, 16000, 0.0022, "CHEMICAL DOSING")
    v.T(E, 400, 15500, 0.0018, "F.L. +149.7m")

    rw0, rw1 = 6075, 6075 + 2870
    v.R(E, rw0, 0, rw1, 18625)
    for xx in (220, 620, 2070):
        v.L(E, rw0 + xx, 0, rw0 + xx, 18625)
    v.T(E, rw0 + 300, 16000, 0.0022, "RECEIVING WELL")

    fb0, fb1 = rw1, rw1 + 9800
    v.R(E, fb0, 0, fb1, 18625)
    v.L(E, fb0 + 4700, 0, fb0 + 4700, 18625)
    v.L(E, fb0 + 4900, 0, fb0 + 4900, 18625)
    v.L(E, fb0 + 8800, 0, fb0 + 8800, 18625)
    for r in range(1, 4):
        v.L(E, fb0, r * 4300, fb1, r * 4300)
    v.T(E, fb0 + 300, 16000, 0.0022, "FLOCCULATION BASIN")
    v.T(E, fb0 + 300, 15500, 0.0018, "8 FLOCCULATOR/MIXER UNITS")

    sb0 = fb1
    sb1 = sb0 + 58750
    sy1 = 19250
    v.R(E, sb0, 0, sb1, sy1)
    for yy in (950, 1200, 5900, 9625, 13350, 18050, 18300):
        v.L(E, sb0, yy, sb1, yy)
    tw = 380
    for row in range(4):
        for col in range(4):
            tx = sb0 + 600 + col * 1500
            ty = 600 + row * 1500
            v.R(E, tx, ty, tx + tw, ty + tw)
    v.T(E, sb0 + 400, sy1 - 2600, 0.0022, "SEDIMENTATION BASIN")
    v.T(E, sb0 + 400, sy1 - 3200, 0.0018, "16 WATER TROUGHS (schematic) - OVERALL 58750")
    v.R(E, sb0 + 700, 700, sb1 - 700, sy1 - 700)
    v.L(E, sb0 + 700, sy1 / 2, sb1 - 700, sy1 / 2)
    v.T(E, sb0 + 1200, sy1 - 4200, 0.0016, "WALKWAY - PLAIN CONCRETE (perimeter + 2 longitudinal)")

    layer_set(E, DASH)
    v.R(E, -400, -1000, sb1 + 1000, sy1 + 1000)
    layer_set(E, "A-WTP-2_GEOM")

    for gx in (0, rw0, fb1, sb1):
        v.L(E, gx, 0, gx, sy1)
    for letter, gx in (("1", 0), ("2", rw0), ("3", fb1), ("4", sb1)):
        grid_bubble(E, *v.P(gx, sy1 + 900), letter)

    section_callout(E, *v.P(3000, sy1 + 2600), "A", 0.02)
    section_callout(E, *v.P(fb0 + 3000, sy1 + 2600), "B", 0.02)
    section_callout(E, *v.P(sb0 + 20000, sy1 + 2600), "C", 0.02)

    dim_y = -2400
    v.DIMH(E, 0, 6075, dim_y, "6075", 0)
    v.DIMH(E, 6075, rw1, dim_y, "2870", 0)
    v.DIMH(E, rw1, fb1, dim_y, "9800", 0)
    v.DIMH(E, fb1, sb1, dim_y, "58750", 0)
    v.DIMH(E, 0, sb1, dim_y - 900, "OVERALL 77495", 0)
    v.DIMV(E, 0, 18625, -2600, "18625", 0)
    v.DIMV(E, 0, sy1, sb1 + 2600, "19250", sb1)

    north_arrow(E, *v.P(sb1 - 9000, sy1 + 4200), r=0.010)
    text(E, 0.015, 0.472, 0.0030, "A-WTP-2  -  PLAN @ EL. +151.0m")
    text(E, 0.015, 0.460, 0.0020,
         "CHEMICAL DOSING / RECEIVING WELL / FLOCCULATION BASIN / SEDIMENTATION BASIN (UPPER LEVEL)")
    reconstruction_note(E, "SCALE APPROX 1:142; WALKWAYS/TROUGHS SCHEMATIC - VERIFY (SPEC ITEM 1)")
    return E


def sheet3():
    E = []
    setup(E)
    title_block(E, 3, ["ELEVATION", "A"], "ELEVATION A")
    v = V(98000, 0.015, 0.100)
    layer_set(E, "A-WTP-3_GEOM")

    BOF = 0.0
    FL = 3.35
    CDFL = 2.05
    Y = lambda m: m * 1000.0

    v.L(E, 0, Y(FL), 58750, Y(FL))
    v.L(E, 0, Y(CDFL), 2600 + 1200, Y(CDFL))
    v.L(E, 0, Y(BOF), 58750, Y(BOF))
    v.L(E, 0, Y(0.55), 58750, Y(-0.25))
    v.T(E, 300, Y(-0.75), 0.0018, "NGL (SLOPING - NO SPOT ELEV)")

    rw1 = 2600
    fb1 = rw1 + 200 + 4700 + 200 + 4700
    sb1 = fb1 + 200 + 45100
    v.L(E, 0, Y(FL), rw1, Y(FL))
    v.L(E, rw1, Y(BOF), rw1, Y(FL))
    v.L(E, rw1, Y(FL), fb1, Y(FL))
    v.L(E, fb1, Y(BOF), fb1, Y(FL))
    v.L(E, fb1, Y(FL), sb1, Y(FL))
    v.L(E, sb1, Y(BOF), sb1, Y(FL))
    v.L(E, sb1, Y(FL), 58750, Y(FL))
    v.L(E, 58750, Y(BOF), 58750, Y(FL))
    v.L(E, 0, Y(BOF), 58750, Y(BOF))

    col_top = Y(FL) + 3150
    for gx in (0, 4500, 9500):
        v.L(E, gx, Y(BOF), gx, col_top)
        v.L(E, gx - 200, Y(FL), gx + 200, Y(FL))
    v.T(E, 100, Y(FL) + 3350, 0.0017, "TOP OF COLUMN - ELEV UNKNOWN (SPEC 6.4)")

    v.L(E, -200, col_top + 400, 3000, col_top + 1200)
    v.L(E, 3000, col_top + 1200, 3000 + 800, col_top + 200)
    v.T(E, 300, col_top + 1750, 0.0018, "SHED ROOF (SLOPED)")

    for gx in range(2800, 57000, 800):
        v.L(E, gx, Y(FL) - 80, gx + 400, Y(FL) + 80)

    fy = Y(BOF)
    v.R(E, 0, fy - 1000, 2600, fy)
    v.R(E, -200, fy - 2650, 2800, fy - 1000)
    v.R(E, -400, fy - 3300, 3000, fy - 2650)
    v.T(E, 300, fy - 4000, 0.0017, "RW FOOTING (SCHEMATIC 250/200/1650/1000/200/1150)")

    grid_bubble(E, *v.P(0, Y(BOF) - 2000), "A'")
    grid_bubble(E, *v.P(4500, Y(BOF) - 2000), "A")
    grid_bubble(E, *v.P(9500, Y(BOF) - 2000), "B")

    v.T(E, 400, Y(FL) + 150, 0.0018, "F.L. +151.00 m (RW/FB/SB)")
    v.T(E, 400, Y(CDFL) + 150, 0.0018, "CD F.L. +149.70 m")
    v.T(E, 400, Y(BOF) - 450, 0.0018, "B.O.F. +147.65 m")

    dy = Y(BOF) - 3300
    v.DIMH(E, 0, rw1, dy, "2600", Y(BOF))
    v.DIMH(E, rw1, fb1, dy, "4700/4700", Y(BOF))
    v.DIMH(E, fb1, sb1, dy, "45100", Y(BOF))
    v.DIMH(E, 0, 58750, dy - 1500, "OVERALL 58750", Y(BOF))
    vx = 58750 + 2500
    v.DIMV(E, Y(BOF), Y(BOF) + 1300, vx, "1300", 58750)
    v.DIMV(E, Y(BOF) + 1300, Y(BOF) + 3250, vx, "1950", 58750)
    v.DIMV(E, Y(BOF) + 3250, Y(BOF) + 3350 + 2215, vx, "2215", 58750)
    v.DIMV(E, Y(FL), col_top, vx, "3150", 58750)

    text(E, 0.015, 0.472, 0.0030, "A-WTP-3  -  ELEVATION A (LONGITUDINAL)")
    reconstruction_note(E, "SCALE APPROX 1:98; TOP-OF-COLUMN & NGL ELEVS UNKNOWN - VERIFY (SPEC 6.4/6.5)")
    return E


def sheet4():
    E = []
    setup(E)
    title_block(E, 4, ["ELEVATION", "B", "SECTION", "C"], "ELEVATION B / SECTION C")
    layer_set(E, "A-WTP-4_GEOM")

    FL = 3.35
    BOF = 0.0
    Y = lambda m: m * 1000.0

    text(E, 0.015, 0.312, 0.0026, "ELEVATION B")
    v2 = V(98000, 0.015, 0.130)
    rw1 = 2400
    fb1 = rw1 + 200 + 4700 + 200 + 4700
    sb1 = fb1 + 200 + 45100
    v2.L(E, 0, Y(FL), rw1, Y(FL))
    v2.L(E, rw1, Y(BOF), rw1, Y(FL))
    v2.L(E, rw1, Y(FL), fb1, Y(FL))
    v2.L(E, fb1, Y(BOF), fb1, Y(FL))
    v2.L(E, fb1, Y(FL), sb1, Y(FL))
    v2.L(E, sb1, Y(BOF), sb1, Y(FL))
    v2.L(E, sb1, Y(FL), 58750, Y(FL))
    v2.L(E, 58750, Y(BOF), 58750, Y(FL))
    v2.L(E, 0, Y(BOF), 58750, Y(BOF))
    v2.L(E, 0, Y(BOF), 0, Y(FL))
    col_top = Y(FL) + 3150
    for gx in (0, 5000, 9500):
        v2.L(E, gx, Y(BOF), gx, col_top)
        v2.L(E, gx - 200, Y(FL), gx + 200, Y(FL))
    grid_bubble(E, *v2.P(0, Y(BOF) - 2000), "B")
    grid_bubble(E, *v2.P(5000, Y(BOF) - 2000), "A")
    grid_bubble(E, *v2.P(9500, Y(BOF) - 2000), "A'")
    v2.T(E, 400, Y(FL) + 150, 0.0018, "F.L. +151.00 m")
    v2.T(E, 400, Y(BOF) - 450, 0.0018, "B.O.F. +147.65 m")
    dy = Y(BOF) - 3200
    v2.DIMH(E, 0, rw1, dy, "2400", Y(BOF))
    v2.DIMH(E, rw1, fb1, dy, "4700/4700", Y(BOF))
    v2.DIMH(E, fb1, sb1, dy, "45100", Y(BOF))
    v2.DIMH(E, 0, 58750, dy - 1400, "OVERALL 58750", Y(BOF))

    text(E, 0.015, 0.272, 0.0026, "SECTION C")
    v3 = V(98000, 0.015, 0.030)
    rw1c = 2400
    fb1c = rw1c + 200 + 4700 + 200 + 4700
    sb1c = fb1c + 200 + 45100
    v3.L(E, 0, Y(FL), rw1c, Y(FL))
    v3.L(E, rw1c, Y(BOF), rw1c, Y(FL))
    v3.L(E, rw1c, Y(FL), fb1c, Y(FL))
    v3.L(E, fb1c, Y(BOF), fb1c, Y(FL))
    v3.L(E, fb1c, Y(FL), sb1c, Y(FL))
    v3.L(E, sb1c, Y(BOF), sb1c, Y(FL))
    v3.L(E, sb1c, Y(FL), 58750, Y(FL))
    v3.L(E, 58750, Y(BOF), 58750, Y(FL))
    v3.L(E, 0, Y(BOF), 58750, Y(BOF))
    v3.L(E, 0, Y(BOF), 0, Y(FL))
    v3.L(E, rw1c, Y(FL) - 2850, sb1c, Y(FL) - 2850)
    v3.T(E, 300, Y(FL) - 2850 - 500, 0.0016, "WATER LEVEL (DEPTH 2850)")
    for px in (rw1c + 500, rw1c + 2500, rw1c + 4500):
        v3.R(E, px, Y(FL) - 900, px + 500, Y(FL) - 300)
        v3.L(E, px + 250, Y(FL), px + 250, Y(FL) - 900)
    v3.T(E, rw1c + 300, Y(FL) - 1200, 0.0016, "FLOCCULATOR PADDLES (SCHEMATIC)")
    grid_bubble(E, *v3.P(0, Y(BOF) - 2000), "A'")
    grid_bubble(E, *v3.P(4500, Y(BOF) - 2000), "A")
    grid_bubble(E, *v3.P(9500, Y(BOF) - 2000), "B")
    dy = Y(BOF) - 3200
    v3.DIMH(E, 0, rw1c, dy, "2400", Y(BOF))
    v3.DIMH(E, rw1c, fb1c, dy, "4700/4700", Y(BOF))
    v3.DIMH(E, fb1c, sb1c, dy, "45100", Y(BOF))
    v3.DIMH(E, 0, 58750, dy - 1400, "OVERALL 58750", Y(BOF))
    v3.DIMV(E, Y(FL) - 2850, Y(FL), 58750 + 2500, "2850", 58750)

    text(E, 0.015, 0.472, 0.0030, "A-WTP-4  -  ELEVATION B / SECTION C")
    reconstruction_note(E, "SCALE APPROX 1:98; VIEW ARRANGEMENT PER SPEC 4a/4b - VERIFY (SPEC ITEM 1)")
    return E


def main():
    outdir = sys.argv[1] if len(sys.argv) > 1 else "."
    sheets = {
        "A-WTP-1": sheet1(),
        "A-WTP-2": sheet2(),
        "A-WTP-3": sheet3(),
        "A-WTP-4": sheet4(),
    }
    plan = {"sheets": {name: {"entities": ents} for name, ents in sheets.items()}}
    path = outdir + "/drawing_plan.json"
    with open(path, "w", encoding="ascii", errors="replace") as f:
        json.dump(plan, f)
    for name, ents in sheets.items():
        print(f"{name}: {len(ents)} entities")


if __name__ == "__main__":
    main()
