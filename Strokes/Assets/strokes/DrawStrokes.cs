using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DrawStrokes : MonoBehaviour {
	public GameObject activeStrokes; //stored meshes
	public GameObject cacheStrokes; //active Drawing mesh.. maybe use array
	Stroke stroke;
	// public Transform debugSphere;
	public Transform cursor;
	public MeshFilter activeMeshFilter;
	public MeshFilter cacheMeshFilter;

	public StrokeOptions strokeOptions;

	public float deltaZ = 0;
    public float deltaRX = 0;

	// Vector3 deltaDraw;
	// Use this for initialization
	void Start () {
        activeMeshFilter = activeStrokes.GetComponent<MeshFilter>();
		cacheMeshFilter = cacheStrokes.GetComponent<MeshFilter>();
		activeMeshFilter.mesh = new Mesh();
		cacheMeshFilter.mesh = new Mesh();
        activeMeshFilter.mesh.name = "activeMesh";
		cacheMeshFilter.mesh.name = "cacheMesh";
		if(strokeOptions==null){
            strokeOptions = new StrokeOptions();
		}
        // strokeOptions = new StrokeOptions();
        //send the mesh to the strokes
        // deltaDraw = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0);
		// List<Vector3> points = GetPoints();
        // stroke.MeshStroke(points);
	}
    List<Vector3> GetPoints(){
        List<Vector3> pts = new List<Vector3>();
		Vector3 current = new Vector3(0,0,0);
		float mag=1;
		for(int i =0; i<200; i++){ 
			pts.Add(current);
			float scale=.1f;
            current = current + new Vector3(Mathf.Cos(i * scale)*mag, Mathf.Sin(i * scale)*mag, 0); //deltaDraw;
            // deltaDraw = deltaDraw + new Vector3(Mathf.Cos(i*.1f), Mathf.Sin(i*.1f), 0);
			mag+=.02f;
		}
		return pts;
	}
	// Update is called once per frame
	void Update () {

        //draw strokes when the mouse is clicked
        Vector3 pos = transform.InverseTransformPoint(cameraMouse());

        deltaZ += .005f;
        Vector3 deltaPos = new Vector3(0, 0, Mathf.Sin(deltaZ) * 2);
        pos += deltaPos;

        cursor.localPosition = pos;
       
		deltaRX += .005f;
        cursor.localEulerAngles = new Vector3 (Mathf.Sin(deltaRX)*90, 0f, 0f);
		Quaternion rotation = cursor.rotation; //world rotation

		if(Input.GetMouseButtonDown(0)){
			stroke = new Stroke(activeMeshFilter.mesh);
            //set the x rotation randomly
            // cursor.localEulerAngles = new Vector3(Random.Range(-60f, 60f), 0, 0);
			stroke.options = strokeOptions;
			stroke.start(pos, rotation);
		}
		if(Input.GetMouseButton(0)){
			stroke.move(pos, rotation);
		}
		if(Input.GetMouseButtonUp(0)){
			stroke.end();
			combineStroke(stroke);
		}
        if (Input.GetKeyDown(KeyCode.Space)){
            clearStrokes();
		}
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
	}
	void clearStrokes(){

	}
	void combineStroke(Stroke _stroke){
		// MeshFilter[] meshFilters = new MeshFilter[2];
		// meshFilters[0] = activeMeshFilter;
		// meshFilters[1] = cacheMeshFilter;
		// CombineInstance[] combine = new CombineInstance[meshFilters.Length];
		// int i = 0;
		// while (i < meshFilters.Length) {
		// 	combine[i].mesh = meshFilters[i].mesh;
		// 	combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
		// 	i++;
		// }

		// Mesh[] meshs = new Mesh[2];
		// meshs[0] = _stroke.mesh;
		// meshs[1] = cacheMeshFilter.mesh;
		CombineInstance[] combine = new CombineInstance[2];
		combine[0].mesh = cacheMeshFilter.mesh;
		combine[0].transform = cacheMeshFilter.transform.localToWorldMatrix;
		combine[1].mesh = _stroke.mesh;
		combine[1].transform = cacheMeshFilter.transform.localToWorldMatrix;

		// int i = 0;
		// while (i < meshFilters.Length) {
		// 	combine[i].mesh = meshFilters[i].mesh;
		// 	combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
		// 	i++;
		// }

		Mesh mergedMesh = new Mesh();
		mergedMesh.name = "mergedMesh";
		Mesh emptyMesh = new Mesh();
		emptyMesh.name = "emptyMesh";

		mergedMesh.CombineMeshes(combine);
		cacheMeshFilter.mesh = mergedMesh;
		//will have to clear out the stroke mesh
		activeMeshFilter.mesh = emptyMesh;
	}
	Vector3 cameraMouse(){
		Plane objPlane = new Plane(Camera.main.transform.forward*-1, this.transform.position);
		Ray mRay = Camera.main.ScreenPointToRay(Input.mousePosition);
		float rayDistance;
		if(objPlane.Raycast(mRay, out rayDistance)){
			// debugSphere.
			return mRay.GetPoint(rayDistance);
		}else
		{
			return new Vector3();
		}
	}
}
