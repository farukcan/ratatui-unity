#!/usr/bin/env python3
"""Strip unsupported wasm features from .o files inside a .a (AR archive).

Removes 'bulk-memory-opt' and 'call-indirect-overlong' feature entries from
the wasm 'target_features' custom section so that older wasm-opt versions
(< v121) do not choke on unknown --enable-* flags.

Usage: python3 strip_wasm_features.py <path/to/lib.a>
"""

import struct
import sys
import os
import tempfile
import subprocess

FEATURES_TO_STRIP = {b"bulk-memory-opt", b"call-indirect-overlong"}


def patch_wasm_object(data: bytes) -> bytes:
    """Patch a single wasm object, removing unwanted target_features entries."""
    if len(data) < 8 or data[:4] != b"\x00asm":
        return data

    pos = 8  # skip magic + version
    chunks = []
    chunks.append(data[:8])

    while pos < len(data):
        section_id = data[pos]
        pos += 1
        section_len, consumed = read_uleb128(data, pos)
        pos += consumed
        section_end = pos + section_len

        if section_id == 0:  # custom section
            name_len, name_consumed = read_uleb128(data, pos)
            name_start = pos + name_consumed
            name = data[name_start : name_start + name_len]

            if name == b"target_features":
                payload_start = name_start + name_len
                new_payload = rewrite_target_features(
                    data[payload_start:section_end]
                )
                if new_payload is not None:
                    new_name_section = (
                        encode_uleb128(len(name)) + name + new_payload
                    )
                    new_section = bytes([0]) + encode_uleb128(
                        len(new_name_section)
                    ) + new_name_section
                    chunks.append(new_section)
                    pos = section_end
                    continue

        # keep section as-is
        chunk_start = pos - consumed - 1
        chunks.append(data[chunk_start:section_end])
        pos = section_end

    return b"".join(chunks)


def rewrite_target_features(payload: bytes) -> bytes | None:
    """Remove unwanted features from target_features payload. Returns None if unchanged."""
    pos = 0
    count, consumed = read_uleb128(payload, pos)
    pos += consumed

    features = []
    changed = False
    for _ in range(count):
        prefix = payload[pos]
        pos += 1
        name_len, consumed = read_uleb128(payload, pos)
        pos += consumed
        name = payload[pos : pos + name_len]
        pos += name_len

        if name in FEATURES_TO_STRIP:
            changed = True
            continue
        features.append((prefix, name))

    if not changed:
        return None

    parts = [encode_uleb128(len(features))]
    for prefix, name in features:
        parts.append(bytes([prefix]))
        parts.append(encode_uleb128(len(name)))
        parts.append(name)
    return b"".join(parts)


def read_uleb128(data: bytes, pos: int) -> tuple[int, int]:
    result = 0
    shift = 0
    consumed = 0
    while True:
        byte = data[pos]
        pos += 1
        consumed += 1
        result |= (byte & 0x7F) << shift
        if (byte & 0x80) == 0:
            break
        shift += 7
    return result, consumed


def encode_uleb128(value: int) -> bytes:
    result = []
    while True:
        byte = value & 0x7F
        value >>= 7
        if value != 0:
            byte |= 0x80
        result.append(byte)
        if value == 0:
            break
    return bytes(result)


def patch_ar_archive(path: str) -> None:
    """Patch all wasm .o files inside an AR archive in-place."""
    with open(path, "rb") as f:
        ar_data = f.read()

    if not ar_data.startswith(b"!<arch>\n"):
        print(f"Error: {path} is not an AR archive", file=sys.stderr)
        sys.exit(1)

    pos = 8
    members = []
    while pos < len(ar_data):
        header = ar_data[pos : pos + 60]
        if len(header) < 60:
            break
        name = header[0:16]
        size_str = header[48:58].strip()
        size = int(size_str)
        data_start = pos + 60
        data_end = data_start + size

        member_data = ar_data[data_start:data_end]
        patched = patch_wasm_object(member_data)
        members.append((header, patched))

        pos = data_end
        if pos % 2 != 0:
            pos += 1  # AR padding

    # Rewrite the archive
    with open(path, "wb") as f:
        f.write(b"!<arch>\n")
        for header, data in members:
            # Update size in header
            new_size = str(len(data)).ljust(10).encode("ascii")
            new_header = header[:48] + new_size + header[58:]
            f.write(new_header)
            f.write(data)
            if len(data) % 2 != 0:
                f.write(b"\n")

    print(f"Patched: {path}")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print(f"Usage: {sys.argv[0]} <path/to/lib.a>", file=sys.stderr)
        sys.exit(1)
    patch_ar_archive(sys.argv[1])
