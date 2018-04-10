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
    float mMinSegmentLength = .1f; // min distance to space vertices
    float mMinDistance = .1f; //minimum distance to add a new segment
    bool mDrawing = false;
    // bool mOptionsChanged = false;
    // bool mGeometryChanged = false;

    int mId = -1;
    int mNumUsedVertices = 0;
    float mStrokeLength = 0;

    Rect mBounds;

    public StrokeOptions options;
    // public float baseThickness = .01f;
	// public float minThickness = .01f;
	// public float maxThickness = 2f;
	// public float speedToThickness = .3f;
	// public int lineCapPoints = 10;
    List<Vector3> mInputPoints;
    List<Quaternion> mInputRotations;
    List<Vector3> mSampledPoints;
    List<Quaternion> mSampledRotations;
    List<Vector3> mOffsets;
    List<float> mThickness;
    
	public Stroke(Mesh _mesh){
        options = new StrokeOptions();
        //initialize all lists
        mInputPoints = new List<Vector3>();
        mInputRotations = new List<Quaternion>();
        mSampledPoints= new List<Vector3>();
        mSampledRotations = new List<Quaternion>(); 
        mOffsets= new List<Vector3>();
        mThickness= new List<float>();
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
public void start(Vector3 pos, Quaternion rotation)
{
    mDrawing = true;
    move(pos, rotation);
}

public void move(Vector3 pos, Quaternion rotation)
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
        // pathPnts[0] = prev2;
        // pathPnts[1] = prev1;
        // pathPnts[2] = cur;
		CubicBezierPath path = new CubicBezierPath(pathPnts);

        // divide segment adaptatively depending on its length
        // save vertices and thickness
        float pathLength = path.ComputeApproxLength();
        int divisions = (int)(pathLength / mMinSegmentLength);

        for (int i = 1; i <= divisions; i++)
        {
            float t = i / (float)divisions;
            float thick = Mathf.Lerp(prevThick, currThick, t);
            Vector3 sampledPoint = path.GetPointNorm(t);
            Quaternion sampledRot = Quaternion.Lerp(lastRot, rotation, t);
			Vector3 norm = Vector3.Normalize( newPos - (mSampledPoints.Count>0 ? mSampledPoints[mSampledPoints.Count-1] : mInputPoints[mInputPoints.Count-1]));
			Vector3 perp = new Vector3(-norm.y, norm.x, norm.z);
            Vector3 rotatedPerp = sampledRot * perp;

            mSampledPoints.Add(sampledPoint);
            mSampledRotations.Add(sampledRot);
            mThickness.Add(thick);
            mOffsets.Add(rotatedPerp);
        }

        // mGeometryChanged = true;
        mInputPoints.Add(newPos);
        mInputRotations.Add(newRot);
		//update the bounds
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
            for (int i = 0; i < numPoints; i++)
            {
                
                float p = Mathf.Min(i, numPoints - i - 1);
                float x = 1.0f -Mathf.Clamp(p / (float)options.lineCapPoints, 0.0f, 1.0f);
                float lineCap = Mathf.Sqrt(1 - Mathf.Pow(x, 2));
                // float lineCap = 1;

                float thick = Mathf.Clamp(options.baseThickness + mThickness[i] * options.speedToThickness, options.minThickness, options.maxThickness);
                //set the vertices:
                //base point - the perpendicular offset * thickness
                vertices[i*2] = mSampledPoints[i]-mOffsets[i] *thick*lineCap;
                //base point + the perpendicular offset *thickness
                vertices[i * 2 +1] = mSampledPoints[i] + mOffsets[i] * thick*lineCap;
            }
            mesh.vertices = vertices;

            //set uvs
            Vector2[] uvs = new Vector2[vertices.Length];
            for (int k = 0; k < uvs.Length; k+=2)
            {
                uvs[k] = new Vector2(k/(float)uvs.Length, 0);
                uvs[k+1] = new Vector2(k/(float)uvs.Length, 1);
            }
            mesh.uv = uvs;

            //build the triangles
            int[] triangles = new int[(numPoints * 2 - 2) * 3];
            int j = 0;
            for (int i = 0; i < numPoints * 2 - 3; i += 2, j++)
            {
                triangles[i * 3] = j * 2;
                triangles[i * 3 + 1] = j * 2 + 1;
                triangles[i * 3 + 2] = j * 2 + 2;

                triangles[i * 3 + 3] = j * 2 + 1;
                triangles[i * 3 + 4] = j * 2 + 3;
                triangles[i * 3 + 5] = j * 2 + 2;

            }
            mesh.triangles = triangles;

            mesh.RecalculateNormals();
        }
	}
}
