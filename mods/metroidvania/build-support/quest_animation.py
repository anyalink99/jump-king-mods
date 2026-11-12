"""Sample generic Unity transform clips for offline sprite baking."""
import bisect
import math
import struct
import numpy as np


def transform(position, rotation, scale):
    x, y, z, w = rotation
    length = math.sqrt(x*x+y*y+z*z+w*w)
    if length == 0:
        raise ValueError('Zero animation quaternion')
    x, y, z, w = x/length, y/length, z/length, w/length
    result = np.eye(4)
    result[:3, :3] = [[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
                       [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
                       [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]]
    result[:3, :3] *= np.asarray(scale)[None, :]
    result[:3, 3] = position
    return result


class Clip:
    def __init__(self, data):
        muscle = data['m_MuscleClip']
        self.duration = muscle['m_StopTime']-muscle['m_StartTime']
        self.start = muscle['m_StartTime']
        clip = muscle['m_Clip']['data']
        streamed = clip['m_StreamedClip']
        self.curves = [[] for _ in range(streamed['curveCount'])]
        raw = struct.pack('<%dI' % len(streamed['data']), *streamed['data'])
        offset = 0
        while offset < len(raw):
            time, count = struct.unpack_from('<fi', raw, offset)
            offset += 8
            if count < 0 or offset+count*20 > len(raw):
                raise ValueError('Truncated streamed animation')
            for _ in range(count):
                index, a, b, c, value = struct.unpack_from('<i4f', raw, offset)
                offset += 20
                if index < 0 or index >= len(self.curves):
                    raise ValueError('Invalid animation channel')
                self.curves[index].append((time, (a, b, c, value)))
        self.times = [[key[0] for key in curve] for curve in self.curves]
        self.dense = clip['m_DenseClip']
        self.constants = clip['m_ConstantClip']['data']
        self.bindings = []
        offset = 0
        for binding in data['m_ClipBindingConstant']['genericBindings']:
            attribute = binding['attribute']
            if binding['typeID'] != 4 or attribute not in (1, 2, 3) or binding['isPPtrCurve']:
                raise ValueError('Only generic transform animation is supported')
            count = 4 if attribute == 2 else 3
            self.bindings.append((binding['path'], attribute, offset, count))
            offset += count
        if offset != len(self.curves)+self.dense['m_CurveCount']+len(self.constants):
            raise ValueError('Animation binding/channel mismatch')

    def sample(self, time):
        time = min(max(time, 0), self.duration)+self.start
        values = []
        for times, keys in zip(self.times, self.curves):
            if not keys:
                raise ValueError('Empty streamed animation channel')
            index = max(0, bisect.bisect_right(times, time)-1)
            at, (a, b, c, value) = keys[index]
            dt = max(0, time-at)
            # sentinel keys carry the boundary value, not a meaningful polynomial
            values.append(value if not math.isfinite(at) or abs(at)>1e20 else ((a*dt+b)*dt+c)*dt+value)
        dense = self.dense
        count = dense['m_CurveCount']
        if count:
            frame = max(0, min(dense['m_FrameCount']-1, (time-dense['m_BeginTime'])*dense['m_SampleRate']))
            first = int(frame)
            second = min(first+1, dense['m_FrameCount']-1)
            for channel in range(count):
                a = dense['m_SampleArray'][first*count+channel]
                b = dense['m_SampleArray'][second*count+channel]
                values.append(a+(b-a)*(frame-first))
        values.extend(self.constants)
        if not np.isfinite(values).all():
            raise ValueError('Non-finite sampled animation')
        return {(path, attr): np.asarray(values[start:start+count]) for path, attr, start, count in self.bindings}
