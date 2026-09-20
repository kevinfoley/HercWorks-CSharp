#!/usr/bin/env python3
"""Dump the Borland class-descriptor records a DBSIM/VSHELL binary carries.

Both executables were built with Borland C++, whose streaming support emits one
descriptor record per participating class. Each record names the class, so this
recovers the *authentic* class names rather than invented ones -- and it also
gives the object size, which settles "how big is this thing" questions that a
constructor's `operator new` argument would otherwise have to answer one at a
time.

Record layout, all little-endian:

    +0x00  uint32  object size in bytes
    +0x04  uint16  always 3
    +0x06  uint16  offset of the name field within the record (0x20 or 0x30)
    +0x08  int32   offset of the primary vtable pointer within the OBJECT
                   (0x17 for the cockpit widget family; -1 when the class has
                   no vtable)
    +0x28  uint32  destructor          (only when the name field is at 0x30)
    +nameOff       NUL-terminated class name
    after the name, rounded up to a dword:
                   pointer to the base class's record, 0 for a root

A class's record pointer is stored **12 bytes before its primary vtable**, so
the vtable block reads

    [record ptr][0][0][primary vtable][subobject offsets][secondary vtable]

which is how a vtable found in the disassembly can be turned into a class name:
read the dword at vtable-0xc and look it up here. `--vtables` does that in
reverse, printing the vtable address alongside each class.

Usage:
    python tools/scripts/es2_classes.py
    python tools/scripts/es2_classes.py --binary VSHELL
    python tools/scripts/es2_classes.py --vtables --filter Gadget
    python tools/scripts/es2_classes.py --tree

This reads the shipped executable, not a Ghidra export, so it needs nothing to
have been analysed first.
"""

from __future__ import annotations

import argparse
import os
import re
import struct
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BINARIES = {
    "DBSIM": os.path.join(REPO, "ES2", "DBSIM.EXE"),
    "VSHELL": os.path.join(REPO, "ES2", "VSHELL.EXE"),
}

NAME = re.compile(rb"[A-Za-z_][A-Za-z0-9_ *]{2,40}\x00")
NAME_OFFSETS = (0x20, 0x30)


class Image:
    def __init__(self, path: str):
        self.data = open(path, "rb").read()
        d = self.data
        pe = struct.unpack_from("<I", d, 0x3C)[0]
        nsec = struct.unpack_from("<H", d, pe + 6)[0]
        optsize = struct.unpack_from("<H", d, pe + 20)[0]
        self.base = struct.unpack_from("<I", d, pe + 24 + 28)[0]
        self.sections = []
        for i in range(nsec):
            o = pe + 24 + optsize + i * 40
            vsize, vaddr, rsize, raw = struct.unpack_from("<IIII", d, o + 8)
            self.sections.append((vaddr, max(vsize, rsize), raw, rsize))

    def va_of(self, off: int):
        for vaddr, _size, raw, rsize in self.sections:
            if raw <= off < raw + rsize:
                return self.base + vaddr + (off - raw)
        return None

    def off_of(self, va: int):
        for vaddr, size, raw, rsize in self.sections:
            if self.base + vaddr <= va < self.base + vaddr + size:
                off = raw + (va - self.base - vaddr)
                return off if off < len(self.data) else None
        return None

    def dword(self, va: int):
        off = self.off_of(va)
        return None if off is None or off + 4 > len(self.data) else struct.unpack_from("<I", self.data, off)[0]


def scan(img: Image):
    """Every descriptor record in the image, keyed by virtual address."""
    out = {}
    d = img.data
    for m in re.finditer(b"\x03\x00", d):
        rec = m.start() - 4
        if rec < 0 or rec + 0x60 > len(d):
            continue
        name_off = struct.unpack_from("<H", d, rec + 6)[0]
        if name_off not in NAME_OFFSETS:
            continue
        size = struct.unpack_from("<I", d, rec)[0]
        if not 4 <= size <= 0x400:
            continue
        nm = NAME.match(d, rec + name_off, rec + name_off + 42)
        if not nm:
            continue
        va = img.va_of(rec)
        if va is None:
            continue
        name = nm.group(0)[:-1].decode("latin1")
        vptr = struct.unpack_from("<i", d, rec + 8)[0]
        # The records are byte-packed in the image, so the pad after the name is
        # relative to the record, not to an absolute dword boundary.
        after = rec + ((name_off + len(name) + 1 + 3) & ~3)
        base = struct.unpack_from("<I", d, after)[0] if after + 4 <= len(d) else 0
        dtor = struct.unpack_from("<I", d, rec + 0x28)[0] if name_off == 0x30 else 0
        out[va] = {"name": name, "size": size, "vptr": vptr, "base": base, "dtor": dtor}
    return out


def find_vtables(img: Image, recs):
    """record VA -> the primary vtable whose preceding dword points at it."""
    out = {}
    for va in recs:
        for m in re.finditer(re.escape(struct.pack("<I", va)), img.data):
            hit = img.va_of(m.start())
            if hit is None:
                continue
            # A vtable block stores the pointer 12 bytes ahead of the table, with
            # two zero dwords between. That pair is what tells a block apart from
            # another record's base-class field.
            if img.dword(hit + 4) == 0 and img.dword(hit + 8) == 0:
                out.setdefault(va, []).append(hit + 0xC)
    return out


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--binary", default="DBSIM", choices=sorted(BINARIES))
    ap.add_argument("--filter", help="only classes whose name contains this (case-insensitive)")
    ap.add_argument("--vtables", action="store_true", help="also resolve each class's primary vtable")
    ap.add_argument("--tree", action="store_true", help="print as an inheritance tree instead of a list")
    args = ap.parse_args()

    img = Image(BINARIES[args.binary])
    recs = scan(img)
    vts = find_vtables(img, recs) if args.vtables else {}
    keep = [va for va, r in recs.items() if not args.filter or args.filter.lower() in r["name"].lower()]

    if args.tree:
        kids = {}
        for va in recs:
            kids.setdefault(recs[va]["base"] if recs[va]["base"] in recs else 0, []).append(va)
        shown = set()

        def walk(parent, depth):
            for va in sorted(kids.get(parent, []), key=lambda v: recs[v]["name"]):
                if va in shown:
                    continue
                shown.add(va)
                print("%s%-28s %08x size=0x%x" % ("  " * depth, recs[va]["name"], va, recs[va]["size"]))
                walk(va, depth + 1)

        walk(0, 0)
        return 0

    print("%d class descriptor record(s) in %s" % (len(keep), args.binary))
    for va in sorted(keep, key=lambda v: recs[v]["name"]):
        r = recs[va]
        base = recs[r["base"]]["name"] if r["base"] in recs else ("%08x" % r["base"] if r["base"] else "-")
        line = "%-28s %08x size=0x%-5x vptr=%-6s base=%s" % (
            r["name"], va, r["size"], ("+0x%x" % r["vptr"]) if r["vptr"] >= 0 else "none", base)
        if args.vtables:
            line += "  vtable=" + ",".join("%08x" % v for v in vts.get(va, [])) if va in vts else "  vtable=-"
        print(line)
    return 0


if __name__ == "__main__":
    sys.exit(main())
