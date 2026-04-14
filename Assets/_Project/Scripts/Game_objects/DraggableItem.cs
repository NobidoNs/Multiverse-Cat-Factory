using System;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider))]
public class DraggableItem : MonoBehaviour
{
    private const float MaxRaycastDistance = 1000f;

    public static bool IsAnyItemDragging { get; private set; }

    [SerializeField] private Camera targetCamera;
    [SerializeField] private float surfaceOffset = 0.05f;
    [SerializeField] private bool disableGravityWhileDragging = true;
    [SerializeField] private bool freezeRotationWhileDragging = true;

    private Rigidbody rb;
    private Collider cachedCollider;
    private bool isDragging;
    private float fallbackPlaneHeight;
    private Vector3 dragOffset;
    private bool originalUseGravity;
    private bool originalIsKinematic;
    private RigidbodyConstraints originalConstraints;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cachedCollider = GetComponent<Collider>();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (!isDragging)
        {
            return;
        }

        if (!Input.GetMouseButton(0))
        {
            EndDrag();
            return;
        }

        UpdateDragPosition();
    }

    private void OnMouseDown()
    {
        if (Input.GetKey(KeyCode.LeftShift))
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        BeginDrag();
    }

    private void OnDisable()
    {
        if (isDragging)
        {
            EndDrag();
        }
    }

    private void BeginDrag()
    {
        Camera cam = GetCamera();
        if (cam == null)
        {
            Debug.LogWarning("DraggableItem: camera is not assigned.");
            return;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!TryGetDragPoint(ray, true, out Vector3 hitPoint))
        {
            return;
        }

        dragOffset = transform.position - hitPoint;
        dragOffset.y = 0f;
        fallbackPlaneHeight = transform.position.y;

        if (rb != null)
        {
            originalUseGravity = rb.useGravity;
            originalIsKinematic = rb.isKinematic;
            originalConstraints = rb.constraints;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;

            if (disableGravityWhileDragging)
            {
                rb.useGravity = false;
            }

            if (freezeRotationWhileDragging)
            {
                rb.constraints = originalConstraints | RigidbodyConstraints.FreezeRotation;
            }
        }

        if (cachedCollider != null)
        {
            cachedCollider.enabled = false;
        }

        isDragging = true;
        IsAnyItemDragging = true;
    }

    private void UpdateDragPosition()
    {
        Camera cam = GetCamera();
        if (cam == null)
        {
            return;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!TryGetDragPoint(ray, false, out Vector3 dragPoint) &&
            !TryGetFallbackPlanePoint(ray, out dragPoint))
        {
            return;
        }

        Vector3 targetPosition = dragPoint + dragOffset;
        targetPosition.y = dragPoint.y + surfaceOffset;

        transform.position = targetPosition;
    }

    private void EndDrag()
    {
        if (cachedCollider != null)
        {
            cachedCollider.enabled = true;
        }

        if (rb != null)
        {
            rb.isKinematic = originalIsKinematic;
            rb.useGravity = originalUseGravity;
            rb.constraints = originalConstraints;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        isDragging = false;
        IsAnyItemDragging = false;
    }

    private bool TryGetDragPoint(Ray ray, bool allowSelfHit, out Vector3 point)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, MaxRaycastDistance);
        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            if (!allowSelfHit && IsSelfCollider(hits[i].collider))
            {
                continue;
            }

            point = hits[i].point;
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    private bool TryGetFallbackPlanePoint(Ray ray, out Vector3 point)
    {
        Plane plane = new Plane(Vector3.up, new Vector3(0f, fallbackPlaneHeight, 0f));
        if (plane.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    private bool IsSelfCollider(Collider otherCollider)
    {
        if (otherCollider == null)
        {
            return false;
        }

        return otherCollider == cachedCollider || otherCollider.transform.IsChildOf(transform);
    }

    private Camera GetCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        return targetCamera;
    }
}
