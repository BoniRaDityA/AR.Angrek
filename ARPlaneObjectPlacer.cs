using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARPlaneObjectPlacer : MonoBehaviour
{
    public ARRaycastManager raycastManager;

    private static readonly List<ARRaycastHit> hits = new();
    private GameObject activeObject;
    private GameObject pendingPrefab;
    private bool waitForPlane = false;

    // Menaruh prefab di bidang datar yang terdeteksi pada posisi tengah layar
    public bool PlaceOnCenter(GameObject prefab)
    {
        if (prefab == null || raycastManager == null)
            return false;
    
    Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    bool hitPlane = raycastManager.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon);
    
    if (!hitPlane)
        return false;

    Pose pose = hits[0].pose;
    ClearPlacedObject();

    Vector3 spawnPosition = pose.position;
    Quaternion cameraYaw = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);
    Quaternion spawnRotation = cameraYaw * prefab.transform.rotation;
    activeObject = Instantiate(prefab, pose.position, spawnRotation);
    return true;
    }

    public void Update()
    {
        if (!waitForPlane)
            return;
        
        if (PlaceOnCenter(pendingPrefab))
        {
            waitForPlane = false;
            pendingPrefab = null;
        }
    }

// Menghapus objek 3D yang sedang tampil
    public void ClearPlacedObject()
    {
        if (activeObject != null)
        {
            Destroy(activeObject);
            activeObject = null;
        }
    }
}