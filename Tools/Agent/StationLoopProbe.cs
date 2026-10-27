using System.Text;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Проверка пилота петли A: геометрия двери, NavMesh, капсула, камера.</summary>
public static class StationLoopProbe
{
    static ForestBuilder B() => Object.FindAnyObjectByType<ForestBuilder>();

    public static string Report()
    {
        var b = B();
        if (b == null) return "no ForestBuilder";
        var sb = new StringBuilder();
        sb.AppendLine($"labCenter={b.LabCenter} labDoor={b.LabDoor} labNorthDoor={b.LabNorthDoor}");
        sb.AppendLine($"doorZero={b.LabNorthDoor == Vector3.zero}");

        var path = new NavMeshPath();
        bool hasPath = NavMesh.CalculatePath(b.LabDoor, b.LabNorthDoor, NavMesh.AllAreas, path);
        float length = 0f;
        for (int i = 1; i < path.corners.Length; i++)
            length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        sb.AppendLine($"path={hasPath} status={path.status} length={length:F1} corners={path.corners.Length}");

        bool northOnMesh = NavMesh.SamplePosition(b.LabNorthDoor, out var northHit, 20f, NavMesh.AllAreas);
        bool southOnMesh = NavMesh.SamplePosition(b.LabDoor, out var southHit, 20f, NavMesh.AllAreas);
        sb.AppendLine($"northOnMesh={northOnMesh} northHit={northHit.position} d={Vector3.Distance(northHit.position, b.LabNorthDoor):F2}");
        sb.AppendLine($"southOnMesh={southOnMesh} southHit={southHit.position} d={Vector3.Distance(southHit.position, b.LabDoor):F2}");

        var player = GameObject.Find("Player");
        var cc = player != null ? player.GetComponent<CharacterController>() : null;
        float radius = cc != null ? cc.radius : 0.5f;
        Vector3 p = b.LabNorthDoor + Vector3.up * 1.75f;
        bool blocked = Physics.CheckCapsule(p + Vector3.up * 0.85f, p - Vector3.up * 0.85f,
            radius, ~0, QueryTriggerInteraction.Ignore);
        sb.AppendLine($"capsuleRadius={radius:F2} northBlocked={blocked}");

        var lab = b.LabComplex;
        if (lab != null)
        {
            bool inside = DomeLabComplex.Contains(lab, new Vector3(b.LabCenter.x,
                b.SampleGround(b.LabCenter.x, b.LabCenter.y) + 1f, b.LabCenter.y));
            bool northInside = DomeLabComplex.Contains(lab, b.LabNorthDoor);
            sb.AppendLine($"labParts={lab.parts} interior={lab.interiorParts} exterior={lab.exteriorParts}");
            sb.AppendLine($"centerInside={inside} northInside={northInside}");
        }
        return sb.ToString();
    }

    public static string Teleport(string place)
    {
        var b = B();
        var player = GameObject.Find("Player");
        if (b == null || player == null) return "no builder/player";
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        Vector3 p;
        if (place == "inside")
            p = new Vector3(b.LabCenter.x, b.SampleGround(b.LabCenter.x, b.LabCenter.y) + 1.1f, b.LabCenter.y);
        else if (place == "north")
            p = b.LabNorthDoor + Vector3.up * 1.1f;
        else
            p = b.LabDoor + Vector3.up * 1.1f;

        player.transform.position = p;
        if (cc != null) cc.enabled = true;
        return $"teleport {place} -> {p}";
    }
}
