using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player; // Assign player in Inspector
    public float verticalSmoothSpeed = 0.125f;
    public float minYSize = 5f;
    public float maxYSize = 10f;
    public float sizeChangeSpeed = 2f;
    public float heightThreshold = 10f; // Height at which max size is reached

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (player == null) return;

        // Only follow vertically
        Vector3 targetPosition = new Vector3(transform.position.x, player.position.y, transform.position.z);
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, targetPosition, verticalSmoothSpeed);
        transform.position = smoothedPosition;

        // Adjust orthographic size based on player's height
        float normalizedHeight = Mathf.Clamp01(player.position.y / heightThreshold);
        float targetSize = Mathf.Lerp(minYSize, maxYSize, normalizedHeight);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, Time.deltaTime * sizeChangeSpeed);
    }
}
