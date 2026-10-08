using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Transform camPivot;
    public Camera cam;
    public float speed = 3.5f, sens = 1f, camDist = 3f;
    public bool frozen;

    CharacterController cc;
    float yaw, pitch = 15f, vy;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (frozen) return;

        Vector2 look = MystraInput.Look();
        yaw += look.x * sens;
        pitch = Mathf.Clamp(pitch - look.y * sens, -10f, 60f);
        camPivot.rotation = Quaternion.Euler(pitch, yaw, 0);

        Vector3 dir = -camPivot.forward;
        float d = camDist;
        if (Physics.Raycast(camPivot.position, dir, out RaycastHit hit, camDist)) d = Mathf.Max(0.3f, hit.distance - 0.2f);
        cam.transform.position = camPivot.position + dir * d;
        cam.transform.rotation = camPivot.rotation;

        Vector2 m = MystraInput.Move();
        Quaternion yawRot = Quaternion.Euler(0, yaw, 0);
        Vector3 desired = yawRot * new Vector3(m.x, 0, m.y);
        if (desired.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(desired), 12f * Time.deltaTime);

        vy = cc.isGrounded ? -1f : vy - 20f * Time.deltaTime;
        cc.Move((desired.normalized * speed + Vector3.up * vy) * Time.deltaTime);
    }
}