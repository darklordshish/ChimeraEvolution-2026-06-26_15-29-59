"""Experimental Blender controller for the two explicit support bones.

No automatic game integration. Reconstructs the same transforms used in the
analytical benchmark, and reports actual-vs-requested matrix error.
"""
import bpy,math
from mathutils import Matrix,Vector

def apply_support(arm,angle,axis,config,one=False):
    aw=arm.matrix_world; worst=0
    f=config['follow'];offset=config['offset']
    for suffix in ('','.L'):
        name='опора_подмышки'+suffix
        if name not in arm.pose.bones:raise ValueError('Missing support bone: '+name)
        h=aw@arm.data.bones['плечо'+suffix].head_local
        side=1 if h.x>=0 else -1; h.z-=offset
        full=-side*angle if axis=='Y' else angle
        if one and suffix=='':full=0
        theta=math.radians(full);t=max(0,min(1,(abs(full)-35)/35));blend=t*t*(3-2*t)
        original=math.atan2(f*math.sin(theta),1-f+f*math.cos(theta))
        rot=original+(f*theta-original)*blend
        scale0=math.sqrt((1-f+f*math.cos(theta))**2+(f*math.sin(theta))**2)
        s=scale0+(1-scale0)*blend
        sc=(s,1,s,1) if axis=='Y' else (1,s,s,1)
        desired=Matrix.Translation(h)@Matrix.Rotation(rot,4,axis)@Matrix.Diagonal(sc)@Matrix.Translation(-h)
        b=arm.data.bones[name];pb=arm.pose.bones[name]
        pb.matrix=aw.inverted()@desired@aw@b.matrix_local
        bpy.context.view_layer.update()
        actual=(aw@pb.matrix)@(aw@b.matrix_local).inverted()
        worst=max(worst,max(abs(actual[i][j]-desired[i][j]) for i in range(4) for j in range(4)))
    return worst
