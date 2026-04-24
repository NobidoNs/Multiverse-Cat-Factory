using System;
using System.Collections;
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
    [SerializeField] private float destroyBelowY = 0f;
    [SerializeField] private float destroyAnimationDuration = 0.2f;
    [SerializeField] private float destroyRiseDistance = 0.15f;
    [SerializeField] private float stuckDetectionDelay = 0.75f;
    [SerializeField] private float stuckVelocityThreshold = 0.02f;
    [SerializeField] private float stuckPositionThreshold = 0.02f;
    [SerializeField] private float stuckPenetrationThreshold = 0.01f;

    private Rigidbody rb;
    private Collider cachedCollider;
    private Collider[] itemColliders = Array.Empty<Collider>();
    private readonly Collider[] overlapResults = new Collider[32];
    private bool isDragging;
    private bool isDespawning;
    private float fallbackPlaneHeight;
    private float stuckTimer;
    private Vector3 dragOffset;
    private Vector3 lastPosition;
    private bool originalUseGravity;
    private bool originalIsKinematic;
    private RigidbodyConstraints originalConstraints;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cachedCollider = GetComponent<Collider>();
        itemColliders = GetComponentsInChildren<Collider>(includeInactive: false);
        lastPosition = transform.position;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (isDespawning)
        {
            return;
        }

        if (transform.position.y < destroyBelowY)
        {
            TryDespawnWithAnimation();
            return;
        }

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
        if (isDespawning)
        {
            return;
        }

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

    private void FixedUpdate()
    {
        if (isDespawning)
        {
            return;
        }

        if (transform.position.y < destroyBelowY)
        {
            TryDespawnWithAnimation();
            return;
        }

        UpdateStuckState();
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

    public bool TryDespawnWithAnimation()
    {
        if (isDespawning)
        {
            return false;
        }

        StartCoroutine(AnimateAndDestroy());
        return true;
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

    private void UpdateStuckState()
    {
        if (isDragging || (rb != null && rb.isKinematic))
        {
            stuckTimer = 0f;
            lastPosition = transform.position;
            return;
        }

        bool isEmbedded = IsEmbeddedInOtherCollider();
        bool isNearlyStill = IsNearlyStill();

        if (isEmbedded && isNearlyStill)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= stuckDetectionDelay)
            {
                TryDespawnWithAnimation();
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        lastPosition = transform.position;
    }

    private bool IsNearlyStill()
    {
        if (rb != null)
        {
            return rb.linearVelocity.sqrMagnitude <= stuckVelocityThreshold * stuckVelocityThreshold;
        }

        return (transform.position - lastPosition).sqrMagnitude <= stuckPositionThreshold * stuckPositionThreshold;
    }

    private bool IsEmbeddedInOtherCollider()
    {
        for (int i = 0; i < itemColliders.Length; i++)
        {
            Collider itemCollider = itemColliders[i];
            if (itemCollider == null || !itemCollider.enabled || itemCollider.isTrigger)
            {
                continue;
            }

            Bounds bounds = itemCollider.bounds;
            int hitCount = Physics.OverlapBoxNonAlloc(
                bounds.center,
                bounds.extents,
                overlapResults,
                itemCollider.transform.rotation,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider otherCollider = overlapResults[hitIndex];
                if (otherCollider == null || otherCollider == itemCollider || otherCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (Physics.ComputePenetration(
                        itemCollider,
                        itemCollider.transform.position,
                        itemCollider.transform.rotation,
                        otherCollider,
                        otherCollider.transform.position,
                        otherCollider.transform.rotation,
                        out _,
                        out float distance) &&
                    distance >= stuckPenetrationThreshold)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private IEnumerator AnimateAndDestroy()
    {
        isDespawning = true;
        stuckTimer = 0f;

        if (isDragging)
        {
            EndDrag();
        }

        for (int i = 0; i < itemColliders.Length; i++)
        {
            Collider itemCollider = itemColliders[i];
            if (itemCollider != null)
            {
                itemCollider.enabled = false;
            }
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        Vector3 startPosition = transform.position;
        Vector3 startScale = transform.localScale;
        Quaternion startRotation = transform.rotation;
        Quaternion endRotation = startRotation * Quaternion.Euler(0f, 135f, 0f);
        float duration = Mathf.Max(0.01f, destroyAnimationDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);

            transform.position = startPosition + Vector3.up * (destroyRiseDistance * easedProgress);
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, easedProgress);
            transform.localScale = Vector3.LerpUnclamped(startScale, Vector3.zero, easedProgress);

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}
