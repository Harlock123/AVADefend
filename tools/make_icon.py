#!/usr/bin/env python3
"""Regenerates src/Defender.Avalonia/Assets/defender.ico and defender-256.png from the project's own
ship sprite (Sprites.ShipRight) — original artwork, no external assets. Usage: python3 tools/make_icon.py"""
import struct, zlib, pathlib

ROWS = ["66..............", "C669............", "CC66999999......", "C6669999999999..", ".66999922.......", "..666..........."]
RG, BL = [0, 38, 81, 118, 137, 174, 217, 255], [0, 95, 160, 255]
PAL = {'6': 0xA4, '9': 0xFF, '2': 0x07, 'C': 0xC7}
BG = (16, 16, 40)

def rgb(b): return (RG[b & 7], RG[(b >> 3) & 7], BL[(b >> 6) & 3])

def png(size):
    scale = max(1, size // 18); left = (size - 16 * scale) // 2; top = (size - 6 * scale) // 2
    raw = bytearray()
    for y in range(size):
        raw.append(0)
        for x in range(size):
            sy = (y - top) // scale if y >= top else -1; sx = (x - left) // scale if x >= left else -1
            c = ROWS[sy][sx] if 0 <= sy < 6 and 0 <= sx < 16 else '.'
            raw += bytes(rgb(PAL[c]) if c != '.' else BG) + b'\xff'
    def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    return (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', size, size, 8, 6, 0, 0, 0))
            + chunk(b'IDAT', zlib.compress(bytes(raw), 9)) + chunk(b'IEND', b''))

out = pathlib.Path(__file__).resolve().parent.parent / "src/Defender.Avalonia/Assets"
sizes = [16, 32, 48, 256]; imgs = [png(s) for s in sizes]
offset = 6 + 16 * len(sizes); ico = struct.pack('<HHH', 0, 1, len(sizes))
for s, d in zip(sizes, imgs):
    ico += struct.pack('<BBBBHHII', s % 256, s % 256, 0, 0, 1, 32, len(d), offset); offset += len(d)
(out / "defender.ico").write_bytes(ico + b''.join(imgs))
(out / "defender-256.png").write_bytes(imgs[-1])
