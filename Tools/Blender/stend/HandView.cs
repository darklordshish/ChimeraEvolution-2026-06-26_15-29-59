using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class HandView
{
    // Вызывать после ShotSpecies.Chimera. Сцену и данные не сохраняет.
    public static string Run(string view, bool close)
    {
        var root=GameObject.Find("~ШОТ");
        var camera=root.GetComponentInChildren<Camera>();
        var renderers=root.GetComponentsInChildren<Renderer>();
        var parts=root.GetComponentsInChildren<BodyPartView>();
        var hands=parts.SelectMany(p=>p.GetComponentsInChildren<Renderer>()).Distinct().ToArray();
        if(hands.Length==0) throw new Exception("Деталь кисти не нарисована");
        var b=hands[0].bounds; foreach(var r in hands)b.Encapsulate(r.bounds);
        var all=renderers[0].bounds;foreach(var r in renderers)all.Encapsulate(r.bounds);
        var target=close ? new Vector3(b.max.x-.06f,b.center.y,b.center.z) : all.center;
        var dir=view=="front"?Vector3.forward:view=="back"?Vector3.back:view=="profile"?Vector3.right:new Vector3(1,0,1).normalized;
        camera.transform.position=target+dir*8;
        camera.transform.rotation=Quaternion.LookRotation(-dir,Vector3.up);
        if(close) camera.orthographicSize=Mathf.Max(.20f,b.size.y*.85f);
        return "кисти: "+hands.Length+"; y="+b.min.y.ToString("F5")+".."+b.max.y.ToString("F5")+"; size="+b.size.ToString("F5");
    }
    public static string Check()
    {
        var donor=AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Волк.asset");
        var p=donor.parts.Single(x=>x.seam=="запястье");
        var weights=PartAssembly.Weighted(p);
        if(weights.Length!=1||weights[0]!="пясть")throw new Exception("Чужие веса");
        var vertices=p.mesh.vertices.Select(v=>p.toBody.MultiplyPoint3x4(v)).ToArray();
        var dv=new Vector3[vertices.Length];p.mesh.GetBlendShapeFrameVertices(p.mesh.GetBlendShapeIndex("двуногий"),0,dv,null,null);
        float maxRingError=0, maxRingMove=0;
        foreach(var point in p.ringM)
        {
            var i=Enumerable.Range(0,vertices.Length).OrderBy(j=>(vertices[j]-point).sqrMagnitude).First();
            maxRingError=Mathf.Max(maxRingError,(vertices[i]-point).magnitude);
            maxRingMove=Mathf.Max(maxRingMove,p.toBody.MultiplyVector(dv[i]).magnitude);
        }
        if(p.ringM.Length!=8||maxRingError>1e-5||maxRingMove>1e-5)throw new Exception("Шов не сохранён");
        return "triangles="+p.mesh.triangles.Length/3+"; vertices="+vertices.Length+"; ringError="+maxRingError+"; ringMove="+maxRingMove+"; weighted="+string.Join(",",weights);
    }
    public static string Lowest()
    {
        var root=GameObject.Find("~ШОТ");
        var r=root.GetComponentInChildren<BodyPartView>().GetComponentInChildren<SkinnedMeshRenderer>();
        var m=new Mesh();r.BakeMesh(m);
        var vv=m.vertices.Select(v=>r.transform.TransformPoint(v)).ToArray();
        var i=Enumerable.Range(0,vv.Length).OrderBy(j=>vv[j].y).First();
        var d=AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Волк.asset").parts.Single(p=>p.seam=="запястье");
        var source=d.toBody.MultiplyPoint3x4(d.mesh.vertices[i%d.mesh.vertexCount]);
        var result="lowest="+vv[i].ToString("F6")+"; index="+i+"; source="+source.ToString("F6");
        UnityEngine.Object.DestroyImmediate(m);return result;
    }
    public static string RingFrame(string carrier)
    {
        var d=AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/Волк.asset");
        var c=AssetDatabase.LoadAssetAtPath<SpeciesSO>("Assets/_Chimera/Data/"+carrier+".asset");
        var part=d.parts.Single(p=>p.seam=="запястье");var db=d.bones.Single(b=>b.name=="пясть");
        var cb=c.bones.Single(b=>b.name==PartAssembly.Resolve(c,d,"пясть"));
        var placed=new System.Collections.Generic.Dictionary<string,(Vector3,Quaternion)>();
        var (pos,rot)=SkeletonBuilder.Place(db,d.bones.ToDictionary(b=>b.name),placed);
        var inv=Matrix4x4.TRS(pos,rot,Vector3.one).inverse;
        return string.Join("\n",part.ringM.Select((p,i)=>{var y=inv.MultiplyPoint3x4(p).y;return i+": delta="+(y-db.length).ToString("G9")+"; scale="+(y>db.length?cb.r1/db.r1:cb.r0/db.r0).ToString("F6");}));
    }
}
