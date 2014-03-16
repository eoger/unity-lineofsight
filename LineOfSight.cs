using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public class LineOfSight : MonoBehaviour
{

    public int subdivisions = 20;
    public int maxIterations = 2;
    public float fovBegin = -30;
    public float fovEnd = 30;

    Mesh lightMesh;
    List<Vector2> vertices = new List<Vector2>();
    const float EPSYLON = 0.00001f;

    void Start()
    {
        lightMesh = GetComponent<MeshFilter>().mesh;
    }

    void CastRays(float startAngle, float stopAngle = 2 * Mathf.PI, int iter = 0)
    {
        float step = (((Mathf.PI * 2f) - startAngle) + stopAngle) % (Mathf.PI * 2f) / (float)subdivisions;
        float angle = startAngle;
        int rays = subdivisions + 1;
        RaycastHit[] hits = new RaycastHit[rays];

        // ################################
        // STEP 1 : SHOOT RAYCASTS
        // ################################

        for (int i = 0; i < rays; i++)
        {
            float x = Mathf.Sin(angle);
            float y = Mathf.Cos(angle);
            int ignoreHolder = ~(1 << 2); // Ignore objects wearing the "Ignore Raycast" mask
            Physics.Raycast(transform.position, new Vector3(x, 0, y), out hits[i], Mathf.Infinity, ignoreHolder);

            angle = (angle + step) % (2f * Mathf.PI);
        }

        // ################################
        // STEP 2 : SUBDIVIDE IF NECESSARY
        // ################################

        Vector3 hitPoint = transform.InverseTransformPoint(hits[0].point);
        vertices.Add(new Vector2(hitPoint.x, hitPoint.z));

        for (int i = 1; i < rays; i++)
        {
            if (hits[i].transform != null && hits[i - 1].transform != null)
                if (hits[i].transform != hits[i - 1].transform)
                    if (iter < maxIterations - 1)
                    {
                        float start = (startAngle + (float)(i - 1) * step) % (2f * Mathf.PI);
                        float stop = (start + step) % (2f * Mathf.PI);
                        CastRays(start, stop, iter + 1);
                    }

            hitPoint = transform.InverseTransformPoint(hits[i].point);
            vertices.Add(new Vector2(hitPoint.x, hitPoint.z));
        }
    }

    void Update()
    {
        vertices.Clear();

        // ########################################
        // PHASE 1 : CALCULATE VERTICES
        // ########################################

        float from = (Quaternion.Euler(0, fovBegin, 0) * transform.rotation).eulerAngles.y * Mathf.Deg2Rad;
        float to = (Quaternion.Euler(0, fovEnd, 0) * transform.rotation).eulerAngles.y * Mathf.Deg2Rad;

        Profiler.BeginSample("Recursive raycasting");

        CastRays(from, to);

        Profiler.EndSample();

        Profiler.BeginSample("Duplicates removing");

        for (int i = vertices.Count - 1; i > 0; i--)
        {
            Vector2 relative = new Vector2(vertices[i - 1].x - vertices[i].x, vertices[i - 1].y - vertices[i].y);
            if (relative.SqrMagnitude() < EPSYLON)
            {
                vertices.RemoveAt(i);
            }
        }

        Profiler.EndSample();

        Profiler.BeginSample("Vertices interpolation");

        // Calculate intermediate vertices (we need a multiple of 3)
        vertices.Add(new Vector3(0, 0, 0));
        int differenceToMultiple = Mathf.Abs((3 - ((vertices.Count) % 3)) % 3);
        for (int i = 1; i <= differenceToMultiple; i++)
        {
            vertices.Add(new Vector2((float)(i) * vertices[0].x / (float)(differenceToMultiple + 1), (float)(i) * vertices[0].y / (float)(differenceToMultiple + 1)));
        }

        Profiler.EndSample();

        // ############################################
        // PHASE 2 : GENERATE MESH TRIANGLES
        // ############################################

        Profiler.BeginSample("Triangulation");

        Triangulator triangulator = new Triangulator(vertices);
        int[] triangles = triangulator.Triangulate();

        Profiler.EndSample();


        // ############################################
        // PHASE 3 : UPDATE MESH VERTICES AND TRIANGLES
        // ############################################

        Profiler.BeginSample("Mesh updating");

        Vector3[] vertices3 = new Vector3[vertices.Count];
        for (int i = 0; i < vertices3.Length; i++)
        {
            vertices3[i] = new Vector3(vertices[i].x, 0f, vertices[i].y);
        }


        lightMesh.triangles = new int[0];
        lightMesh.vertices = vertices3;
        lightMesh.triangles = triangles;

        lightMesh.normals = new Vector3[vertices3.Length];
        lightMesh.uv = new Vector2[vertices3.Length];

        // Use this instead if you want lightning on the mesh
        // lightMesh.RecalculateNormals();

        Profiler.EndSample();
    }

}
