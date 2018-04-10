using System.Collections;
using System.Collections.Generic;
using UnityEngine;



[System.Serializable]
public class Stroke {
	public Mesh mesh;
    int mVerticesPerMesh = 600;
    int mPointsPerMesh = 600 / 2;
    float mMinSegmentLength = .1f;//.0f;

    bool mDrawing = false;
    bool mOptionsChanged = false;
    bool mGeometryChanged = false;

    int mId = -1;
    int mNumUsedVertices = 0;
    float mStrokeLength = 0;

    Rect mBounds;
    public float baseThickness = .01f;
	public float minThickness = .01f;
	public float maxThickness = 2f;
	public float speedToThickness = 2f;
	public int lineCapPoints = 10;
    List<Vector3> mInputPoints;
    List<Vector3> mSampledPoints;
    List<Vector3> mOffsets;
    List<float> mThickness;
    
	public Stroke(Mesh _mesh){
        //initialize all lists
        mInputPoints = new List<Vector3>();
        mSampledPoints= new List<Vector3>();
        mOffsets= new List<Vector3>();
        mThickness= new List<float>();
        mesh = _mesh;
	}
    public void BuildStroke(List<Vector3> _points){
        foreach (Vector3 point in _points)
        {
            if (mInputPoints.Count == 0)
            {
                start(point);
            }
            else
            {
                move(point);
            }
        }
        end();
	}
public void start(Vector3 pos)
{
    mDrawing = true;
    move(pos);
}

public void move(Vector3 pos)
{
    if (!mDrawing) return;

    if (mInputPoints.Count <= 2)
    {
        mInputPoints.Add(pos);
    }
    else
    {
		Vector3 lastPos =mInputPoints[mInputPoints.Count - 1];
        Vector3 newPos = Vector3.Lerp(lastPos, pos, .35f);

        // calculate thickness
        float dist = Vector3.Distance(newPos, lastPos);

        if (dist < .1) return;

    //     //        float newThick = clamp(mOpts.baseThickness + dist * mOpts.speedToThickness, mOpts.minThickness, mOpts.maxThickness);
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
		CubicBezierPath path = new CubicBezierPath(pathPnts);

        // divide segment adaptatively depending on its length
        // save vertices and thickness
        float pathLength = path.ComputeApproxLength();
        int divisions = (int)(pathLength / mMinSegmentLength);

        for (int i = 1; i <= divisions; i++)
        {
            float t = i / (float)divisions;
            float thick = Mathf.Lerp(prevThick, currThick, t);
            Vector3 point = path.GetPointNorm(t);
			Vector3 norm = Vector3.Normalize( newPos - (mSampledPoints.Count>0 ? mSampledPoints[mSampledPoints.Count-1] : mInputPoints[mInputPoints.Count-1]));
			Vector3 perp = new Vector3(-norm.y, norm.x, norm.z);

            mSampledPoints.Add(point);
            mThickness.Add(thick);
            mOffsets.Add(perp);
        }

        mGeometryChanged = true;
        mInputPoints.Add(newPos);
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
			Debug.Log("mesh numPoints:"+numPoints);

            //build the vertices
            Vector3[] vertices = new Vector3[numPoints*2];
            for (int i = 0; i < numPoints; i++)
            {
                
                float p = Mathf.Min(i, numPoints - i - 1);
                float x = 1.0f -Mathf.Clamp(p / (float)lineCapPoints, 0.0f, 1.0f);
                float lineCap = Mathf.Sqrt(1 - Mathf.Pow(x, 2));
                // float lineCap = 1;

                float thick = Mathf.Clamp(baseThickness + mThickness[i] * speedToThickness, minThickness, maxThickness);
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
