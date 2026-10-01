using UnityEngine;

// Dev-only inspection camera (?dev=1). Driven from the page console:
//   unityInstance.SendMessage("DevCam", "Set", "x,y,z,pitch,yaw,fov")   // park the camera
//   unityInstance.SendMessage("DevCam", "Follow", "Player,3,2.2,160,15") // orbit a named object: dist,height,yaw,pitch
//   unityInstance.SendMessage("DevCam", "Off", "")
//   unityInstance.SendMessage("DevCam", "Speed", "3")                   // time scale
[DefaultExecutionOrder(10000)]
public class DevCam : MonoBehaviour
{
    bool on; Vector3 pos; Vector3 euler; float fov = 50;
    Transform follow; float fDist, fHeight, fYaw, fPitch;

    public static void Install(bool dev) { if (dev && !FindAnyObjectByType<DevCam>()) new GameObject("DevCam").AddComponent<DevCam>(); }

    static float[] Nums(string s) { var p = s.Split(','); var r = new float[p.Length]; for (int i = 0; i < p.Length; i++) float.TryParse(p[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out r[i]); return r; }

    public void Set(string s) { var n = Nums(s); pos = new Vector3(n[0], n[1], n[2]); euler = new Vector3(n[3], n[4], 0); if (n.Length > 5) fov = n[5]; on = true; follow = null; }
    public void Follow(string s)
    {
        var p = s.Split(',');
        var go = GameObject.Find(p[0]);
        if (!go) { Debug.Log("DevCam: no object " + p[0]); return; }
        follow = go.transform; var n = Nums(s.Substring(p[0].Length + 1));
        fDist = n[0]; fHeight = n[1]; fYaw = n[2]; fPitch = n[3]; if (n.Length > 4) fov = n[4]; on = true;
    }
    public void Off(string s) { on = false; follow = null; }
    public void Speed(string s) { Time.timeScale = Nums(s)[0]; }

    void LateUpdate()
    {
        if (!on) return;
        var cam = Camera.main; if (!cam) return;
        if (follow)
        {
            var q = Quaternion.Euler(0, fYaw, 0);
            var target = follow.position + Vector3.up * fHeight * 0.45f;
            cam.transform.position = target + q * new Vector3(0, fHeight, -fDist);
            cam.transform.rotation = Quaternion.LookRotation(target - cam.transform.position);
        }
        else { cam.transform.position = pos; cam.transform.rotation = Quaternion.Euler(euler); }
        cam.fieldOfView = fov;
    }
}
