using System.Collections;
using System.Collections.Generic;
using UnityEngine;



[System.Serializable]
public class StrokeOptions
{
    //add a general scale
    //add min segment length
    public float baseThickness = .01f;
    public float minThickness = .01f;
    public float maxThickness = 2f;
    public float speedToThickness = .3f;
    public int lineCapPoints = 10;
    public StrokeOptions(
        float _baseThickness = .01f,
        float _minThickness = .01f,
        float _maxThickness = 2f,
        float _speedToThickness = .3f,
        int _lineCapPoints = 10){
            baseThickness = _baseThickness;
            minThickness = _minThickness;
            maxThickness = _maxThickness;
            speedToThickness = _speedToThickness;
            lineCapPoints = _lineCapPoints;
    }
}
[System.Serializable]
public class Stroke {
	public Mesh mesh;
    // int mVerticesPerMesh = 600;
    // int mPointsPerMesh = 600 / 2;
    float mMinSegmentLength = .01f; // min distance to space vertices
    float mMinDistance = .01f; //minimum distance to add a new segment
    bool mDrawing = false;
    // bool mOptionsChanged = false;
    // bool mGeometryChanged = false;
    public bool backFaces = true;
    public float depth= .01f; //how deep the stroke is, used for back face
    int mId = -1;
    int mNumUsedVertices = 0;
    float mStrokeLength = 0;

    public StrokeOptions options;
    List<Vector3> mInputPoints;
    List<Quaternion> mInputRotations;
    List<Vector3> mSampledPoints;
    List<Quaternion> mSampledRotations;
    List<Vector3> mOffsets;
    List<float> mThickness;
    List<float> mPressure;
    
	public Stroke(Mesh _mesh){
        options = new StrokeOptions();
        //initialize all lists
        mInputPoints = new List<Vector3>();
        mInputRotations = new List<Quaternion>();
        mSampledPoints= new List<Vector3>();
        mSampledRotations = new List<Quaternion>(); 
        mOffsets= new List<Vector3>();
        mThickness= new List<float>();
        mPressure = new List<float>();
        mesh = _mesh;
	}
    // public void BuildStroke(List<Vector3> _points){
    //     foreach (Vector3 point in _points)
    //     {
    //         if (mInputPoints.Count == 0)
    //         {
    //             start(point);
    //         }
    //         else
    //         {
    //             move(point);
    //         }
    //     }
    //     end();
	// }
public void start(Vector3 pos, Quaternion rotation, float pressure= 1f)
{
    mDrawing = true;
    move(pos, rotation, pressure);
}

public void move(Vector3 pos, Quaternion rotation, float pressure = 1f)
{
    if (!mDrawing) return;

    if (mInputPoints.Count <= 2)
    {
        mInputPoints.Add(pos);
        mInputRotations.Add(rotation);
    }
    else
    {
        //blend position
		Vector3 lastPos = mInputPoints[mInputPoints.Count - 1];
        Vector3 newPos = Vector3.Lerp(lastPos, pos, .35f);
        float dist = Vector3.Distance(newPos, lastPos);
        //blend rotation
        Quaternion lastRot = mInputRotations[mInputRotations.Count - 1];
        Quaternion newRot = rotation; //Quaternion.Lerp(lastRot, rotation, .35f);

        if (dist < mMinDistance) return;
        // calculate thickness
        float newThick = dist;
        float prevThick = mThickness.Count <1 ? newThick : mThickness[mThickness.Count -1];
        float currThick = Mathf.Lerp(prevThick, newThick, 0.2f); //lerp(prevThick, newThick, 0.2);

        float newPressure = pressure;
        float prevPressure = mPressure.Count < 1 ? newThick : mPressure[mPressure.Count - 1];
        float currPressure = Mathf.Lerp(prevPressure, newPressure, 0.4f); //lerp(prevThick, newThick, 0.2);

        // get last 3 input points
        Vector3 prev2 = mInputPoints[mInputPoints.Count-2];
		Vector3 prev1 = mInputPoints[mInputPoints.Count - 1];
		Vector3 cur = newPos;

		//come back to this bit
		//create bezier segment with inputs as control points
		Vector3[] pathPnts =new Vector3[3];
		pathPnts[0] = (prev2 + prev1) / 2.0f;
		pathPnts[1] = prev1;
		pathPnts[2] = (prev1 + cur) / 2.0f;
		CubicBezierPath path = new CubicBezierPath(pathPnts);

        // divide segment adaptatively depending on its length
        // save vertices and thickness
        float pathLength = path.ComputeApproxLength();
        int divisions = (int)(pathLength / mMinSegmentLength);

        for (int i = 1; i <= divisions; i++)
        {
            float t = i / (float)divisions;
            float thick = Mathf.Lerp(prevThick, currThick, t);
            float pressureStep = Mathf.Lerp(prevPressure, currPressure, t);
            Vector3 sampledPoint = path.GetPointNorm(t);
            Quaternion sampledRot = Quaternion.Lerp(lastRot, rotation, t);
			Vector3 norm = Vector3.Normalize( newPos - (mSampledPoints.Count>0 ? mSampledPoints[mSampledPoints.Count-1] : mInputPoints[mInputPoints.Count-1]));
			Vector3 perp = new Vector3(-norm.y, norm.x, norm.z);
            Vector3 rotatedPerp = sampledRot * perp;

            mSampledPoints.Add(sampledPoint);
            mSampledRotations.Add(sampledRot);
            mThickness.Add(thick);
            mPressure.Add(pressureStep);
            mOffsets.Add(rotatedPerp);
        }

        mInputPoints.Add(newPos);
        mInputRotations.Add(newRot);
        updateMesh();
    }
}

public void end()
{
    if (!mDrawing) return;

    mDrawing = false;
	Debug.Log("end");
    updateMesh();
}
	void updateMesh(){

        int numPoints = mSampledPoints.Count;

        if (numPoints > 0)
        {
            mNumUsedVertices = 0;
			// Debug.Log("mesh numPoints:"+numPoints);

            //build the vertices
            Vector3[] vertices = new Vector3[numPoints*2];
            Vector3[] doubleVerts = new Vector3[vertices.Length * 2];
            for (int i = 0; i < numPoints; i++)
            {
                
                float p = Mathf.Min(i, numPoints - i - 1);
                float x = 1.0f -Mathf.Clamp(p / (float)options.lineCapPoints, 0.0f, 1.0f);
                float lineCap = Mathf.Sqrt(1 - Mathf.Pow(x, 2));
                // float lineCap = 1;

                float thick = Mathf.Clamp(options.baseThickness + mThickness[i] * options.speedToThickness, options.minThickness, options.maxThickness);
                float pressure = mPressure[i]*2.0f;
                //set the vertices:
                //base point - the perpendicular offset * thickness
                vertices[i*2] = mSampledPoints[i]-mOffsets[i] *thick*lineCap*pressure;
                //base point + the perpendicular offset *thickness
                vertices[i * 2 +1] = mSampledPoints[i] + mOffsets[i] * thick*lineCap*pressure;
            }
            // Debug.Log("vertices:" + vertices.Length);
            if (backFaces)
            //double the vertices
            {
                // Vector3[] backFacevertices = (Vector3[])vertices.Clone();
                // Vector3[] backFacevertices = System.Array.Copy(vertices, backFacevertices,) new Vector3[numPoints * 2];
                // backFacevertices[i * 2] = mSampledPoints[i] - mOffsets[i] * thick * lineCap;
                // backFacevertices[i * 2 + 1] = mSampledPoints[i] + mOffsets[i] * thick * lineCap;

                
                vertices.CopyTo(doubleVerts, 0);
                vertices.CopyTo(doubleVerts, vertices.Length);
                mesh.vertices = doubleVerts;
                // Debug.Log("doubleVerts:" + doubleVerts.Length);
            }else{
                mesh.vertices = vertices;
            }

            //set uvs
            if (backFaces){
                Vector2[] uvs = new Vector2[doubleVerts.Length];
                float textureDis = 1f; //every 1 distance the x will wrap?
                float strokeDistance = 0;
                for (int k = 0; k < uvs.Length; k += 2)
                {
                    // float u = k / (float)uvs.Length;
                    float u = strokeDistance / textureDis;
                    uvs[k] = new Vector2(u, 0);
                    uvs[k + 1] = new Vector2(u, 1);
                    if(k-1 >=0){
                        strokeDistance += Vector3.Distance(doubleVerts[k], doubleVerts[k-1]);
                    }
                }
                mesh.uv = uvs;
            }else{
                Vector2[] uvs = new Vector2[vertices.Length];
                for (int k = 0; k < uvs.Length; k += 2)
                {
                    uvs[k] = new Vector2(k / (float)uvs.Length, 0);
                    uvs[k + 1] = new Vector2(k / (float)uvs.Length, 1);
                }
                mesh.uv = uvs;
            }
            

            //build the triangles
            int j = 0;
            //front faces
            int[] triangles = new int[(numPoints * 2 - 2) * 3];
            for (int i = 0; i < numPoints * 2 - 3; i += 2, j++)
            {
                triangles[i * 3] = j * 2;
                triangles[i * 3 + 1] = j * 2 + 1;
                triangles[i * 3 + 2] = j * 2 + 2;

                triangles[i * 3 + 3] = j * 2 + 1;
                triangles[i * 3 + 4] = j * 2 + 3;
                triangles[i * 3 + 5] = j * 2 + 2;

            }
            if(backFaces){
                int[] doubleTriangles = new int[triangles.Length*2];
                // var doubleVerts = new Vector3[vertices.Length * 2];
                triangles.CopyTo(doubleTriangles, 0);
                // System.Array.Reverse(triangles);
                // j = triangles.Length;
                int[] backTriangles = new int[(numPoints * 2 - 2) * 3];
                for (int i = 0; i < numPoints * 2 - 3; i += 2, j++)
                {
                    backTriangles[i * 3 + 0] = j * 2 + 2;
                    backTriangles[i * 3 + 1] = j * 2 + 1;
                    backTriangles[i * 3 + 2] = j * 2;

                    backTriangles[i * 3 + 3] = j * 2 + 2;
                    backTriangles[i * 3 + 4] = j * 2 + 3;
                    backTriangles[i * 3 + 5] = j * 2 + 1;

                }
                backTriangles.CopyTo(doubleTriangles, triangles.Length);

                // int[] backTriangles = new int[(numPoints * 2 - 2) * 6];
                // for (int i = 0; i < numPoints * 2 - 3; i += 2, j++)
                // {
                //     //front faces
                //     triangles[i * 6] = j * 2;
                //     triangles[i * 6 + 1] = j * 2 + 1;
                //     triangles[i * 6 + 2] = j * 2 + 2;

                //     triangles[i * 6 + 3] = j * 2 + 1;
                //     triangles[i * 6 + 4] = j * 2 + 3;
                //     triangles[i * 6 + 5] = j * 2 + 2;

                //     //back faces
                //     triangles[i * 6 + 6] = j * 2 + 2;
                //     triangles[i * 6 + 7] = j * 2 + 1;
                //     triangles[i * 6 + 8] = j * 2;

                //     triangles[i * 6 + 9] = j * 2 + 2;
                //     triangles[i * 6 + 10] = j * 2 + 3;
                //     triangles[i * 6 + 11] = j * 2 + 1;
                // }
                mesh.triangles = doubleTriangles;
            }else{
                mesh.triangles = triangles;
            }
            // int[] triangles = new int[(numPoints * 2 - 2) * 3];
            // int j = 0;
            
           

            mesh.RecalculateNormals();
        }
	}
}
