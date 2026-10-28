import struct
import sys
import unittest
from pathlib import Path
import numpy as np

sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'build-support'))
from quest_animation import Clip,transform


def fixture():
    raw=b''
    for time,value in [(0,0),(1,1)]:
        raw+=struct.pack('<fi',time,3)
        for index in range(3):raw+=struct.pack('<i4f',index,0,0,1 if index==0 else 0,value if index==0 else 0)
    return {'m_MuscleClip':{'m_StartTime':0,'m_StopTime':1,'m_Clip':{'data':{
        'm_StreamedClip':{'curveCount':3,'data':list(struct.unpack('<%dI'%(len(raw)//4),raw))},
        'm_DenseClip':{'m_CurveCount':0},'m_ConstantClip':{'data':[0,0,0,1]}}}},
        'm_ClipBindingConstant':{'genericBindings':[{'path':0,'typeID':4,'attribute':1,'isPPtrCurve':0},{'path':0,'typeID':4,'attribute':2,'isPPtrCurve':0}]}}


class AnimationTests(unittest.TestCase):
    def test_stream_and_constants(self):
        clip=Clip(fixture())
        np.testing.assert_allclose(clip.sample(.25)[0,1],[.25,0,0])
        np.testing.assert_allclose(clip.sample(.25)[0,2],[0,0,0,1])
        np.testing.assert_allclose(clip.sample(5)[0,1],[1,0,0])

    def test_dense_interpolation(self):
        data=fixture();channels=data['m_MuscleClip']['m_Clip']['data']
        channels['m_StreamedClip']={'curveCount':0,'data':[]}
        channels['m_DenseClip']={'m_CurveCount':3,'m_FrameCount':2,'m_SampleRate':1,'m_BeginTime':0,'m_SampleArray':[0,0,0,2,4,6]}
        np.testing.assert_allclose(Clip(data).sample(.5)[0,1],[1,2,3])

    def test_refuse_unsupported_or_broken_channels(self):
        data=fixture();data['m_ClipBindingConstant']['genericBindings'][0]['typeID']=114
        with self.assertRaises(ValueError):Clip(data)
        data=fixture();data['m_MuscleClip']['m_Clip']['data']['m_StreamedClip']['data'].pop()
        with self.assertRaises(ValueError):Clip(data)
        data=fixture();data['m_MuscleClip']['m_Clip']['data']['m_ConstantClip']['data'].pop()
        with self.assertRaises(ValueError):Clip(data)

    def test_transform_has_unity_column_order(self):
        local=transform([2,3,4],[0,0,0,2],[2,3,4])
        np.testing.assert_allclose(local@np.asarray([1,1,1,1]),[4,6,8,1])
        with self.assertRaises(ValueError):transform([0,0,0],[0,0,0,0],[1,1,1])


if __name__=='__main__':unittest.main()
