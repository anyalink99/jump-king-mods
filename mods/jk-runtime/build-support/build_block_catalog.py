"""Pack the reviewed colour catalogue for cheap reads inside the game."""
import json
import struct
import sys
from pathlib import Path


def pack(data):
    result = bytearray(b"JKBC\x01")

    def integer(value):
        result.extend(struct.pack("<i", value))

    def text(value):
        encoded = value.encode("utf-8")
        # BinaryReader.ReadString uses a 7-bit byte length, not a character count
        length = len(encoded)
        while length >= 128:
            result.append((length & 127) | 128)
            length >>= 7
        result.append(length)
        result.extend(encoded)

    def channels(values, count):
        if len(values) != count or any(type(x) is not int or not 0 <= x <= 255 for x in values):
            raise ValueError("Invalid catalogue colour: " + repr(values))
        result.extend(values)

    text(data["auditDate"])
    integer(len(data["factories"]))
    for factory in data["factories"]:
        text(factory["owner"])
        text(factory["factory"])
        integer(len(factory["rgb_ranges"]))
        for value in factory["rgb_ranges"]:
            channels(value, 4)
            if value[2] > value[3]:
                raise ValueError("Reversed catalogue range")
    integer(len(data["reservations"]))
    for reservation in data["reservations"]:
        text(reservation["owner"])
        text(reservation["name"])
        channels(reservation["rgb"], 3)
    return result


if __name__ == "__main__":
    source, destination = map(Path, sys.argv[1:])
    data = json.loads(source.read_text(encoding="utf-8"))
    destination.write_bytes(pack(data))
