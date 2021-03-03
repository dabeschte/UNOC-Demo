using UnityEngine;

public class TrunTableCameraController : MonoBehaviour
{
    public Transform center;
    public float speed = 3.0f;
    public float zoomFactor = 1.2f;
    public float distanceMin = 1.0f;
    public float distanceMax = 10f;
    private Vector3 angles;

    void Start()
    {
        angles = transform.localEulerAngles;
        var distance = (transform.position - center.position).magnitude;
        transform.position = center.position - transform.forward * distance;
    }

    void Update()
    {
//        if (Input.GetMouseButtonDown(0))
//        {
//            Cursor.lockState = CursorLockMode.Locked;
//        }
//
//        if (Input.GetMouseButtonUp(0))
//        {
//            Cursor.visible = true;
//            Cursor.lockState = CursorLockMode.None;
//        }

        if (Input.GetMouseButton(0))
        {
            angles.y += Input.GetAxis("Mouse X") * speed;
            angles.x -= Input.GetAxis("Mouse Y") * speed;
            angles.x = Mathf.Clamp(angles.x, -70, 70);
            transform.localEulerAngles = angles;
        }

        var distance = (transform.position - center.position).magnitude;
        var zoomDelta = Input.mouseScrollDelta.y;
        if (zoomDelta < 0)
        {
            distance *= zoomFactor;
        } else if (zoomDelta > 0)
        {
            distance /= zoomFactor;
        }
        distance = Mathf.Clamp(distance, distanceMin, distanceMax);
        transform.position = center.position - transform.forward * distance;
    }
}
