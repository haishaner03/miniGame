using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    // Larger values make the camera ease into the target instead of feeling locked to it.
    [SerializeField] private float smoothTime = 0.16f;
    [SerializeField] private float maxFollowSpeed = 60f;
    [SerializeField] private Vector2 offset = Vector2.zero;
    [SerializeField] private bool clampToMap = true;
    [SerializeField] private Vector2 mapMin = new Vector2(0f, 0f);
    [SerializeField] private Vector2 mapMax = new Vector2(40f, 48f);

    private Vector3 velocity;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
        if (clampToMap)
        {
            Camera cam = GetComponent<Camera>();
            if (cam != null && cam.orthographic)
            {
                float halfHeight = cam.orthographicSize;
                float halfWidth = halfHeight * cam.aspect;
                desired.x = Mathf.Clamp(desired.x, mapMin.x + halfWidth, mapMax.x - halfWidth);
                desired.y = Mathf.Clamp(desired.y, mapMin.y + halfHeight, mapMax.y - halfHeight);
            }
        }

        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime, maxFollowSpeed);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
