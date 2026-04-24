using System;
using UnityEngine;

public class ProcessingMachine : MonoBehaviour
{
    [SerializeField] private GameObject acceptedInputPrefab;
    [SerializeField] private GameObject outputPrefab;
    [SerializeField] private float processingTime = 1.5f;
    [SerializeField] private Vector3 inputZoneSize = new Vector3(0.8f, 0.8f, 0.35f);
    [SerializeField] private Vector3 outputZoneSize = new Vector3(0.8f, 0.8f, 0.35f);
    [SerializeField] private float sideMargin = 0.05f;
    [SerializeField] private float holdHeight = 0.35f;
    [SerializeField] private float outputHeight = 0.35f;
    [SerializeField] private float outputForwardOffset = 0.1f;

    private readonly Collider[] overlapResults = new Collider[32];
    private Renderer[] cachedRenderers = Array.Empty<Renderer>();
    private Collider[] cachedColliders = Array.Empty<Collider>();

    private GameObject currentInputItem;
    private Rigidbody currentInputBody;
    private bool currentInputHadGravity;
    private bool currentInputWasKinematic;
    private float processingFinishTime;

    private bool IsProcessing => currentInputItem != null;

    private void Awake()
    {
        CacheBoundsSources();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CacheBoundsSources();
        }
    }

    private void FixedUpdate()
    {
        if (!TryGetLocalBounds(out Bounds localBounds))
        {
            return;
        }

        if (!IsProcessing)
        {
            TryCaptureInput(localBounds);
            return;
        }

        KeepInputItemInside(localBounds);

        if (Time.time < processingFinishTime || IsOutputBlocked(localBounds))
        {
            return;
        }

        ReleaseProcessedItem(localBounds);
    }

    private void CacheBoundsSources()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(includeInactive: false);
        cachedColliders = GetComponentsInChildren<Collider>(includeInactive: false);
    }

    private void TryCaptureInput(Bounds localBounds)
    {
        int hitCount = Physics.OverlapBoxNonAlloc(
            GetInputZoneCenter(localBounds),
            inputZoneSize * 0.5f,
            overlapResults,
            transform.rotation,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = overlapResults[i];
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            Rigidbody body = hitCollider.attachedRigidbody;
            GameObject candidate = body != null ? body.gameObject : hitCollider.gameObject;
            if (candidate == null || !MatchesAcceptedInput(candidate))
            {
                continue;
            }

            currentInputItem = candidate;
            currentInputBody = body;

            if (currentInputBody != null)
            {
                currentInputHadGravity = currentInputBody.useGravity;
                currentInputWasKinematic = currentInputBody.isKinematic;
                currentInputBody.linearVelocity = Vector3.zero;
                currentInputBody.angularVelocity = Vector3.zero;
                currentInputBody.useGravity = false;
                currentInputBody.isKinematic = true;
            }

            KeepInputItemInside(localBounds);
            processingFinishTime = Time.time + Mathf.Max(0.05f, processingTime);
            return;
        }
    }

    private void KeepInputItemInside(Bounds localBounds)
    {
        if (currentInputItem == null)
        {
            return;
        }

        currentInputItem.transform.position = GetHoldPosition(localBounds);
    }

    private void ReleaseProcessedItem(Bounds localBounds)
    {
        if (currentInputItem != null)
        {
            Destroy(currentInputItem);
        }

        currentInputItem = null;

        if (currentInputBody != null)
        {
            currentInputBody.useGravity = currentInputHadGravity;
            currentInputBody.isKinematic = currentInputWasKinematic;
            currentInputBody = null;
        }

        if (outputPrefab != null)
        {
            Instantiate(outputPrefab, GetOutputSpawnPosition(localBounds), Quaternion.identity);
        }
    }

    private bool IsOutputBlocked(Bounds localBounds)
    {
        int hitCount = Physics.OverlapBoxNonAlloc(
            GetOutputZoneCenter(localBounds),
            outputZoneSize * 0.5f,
            overlapResults,
            transform.rotation,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = overlapResults[i];
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            Rigidbody body = hitCollider.attachedRigidbody;
            GameObject candidate = body != null ? body.gameObject : hitCollider.gameObject;
            if (candidate != null && candidate != currentInputItem)
            {
                return true;
            }
        }

        return false;
    }

    private bool MatchesAcceptedInput(GameObject candidate)
    {
        if (acceptedInputPrefab == null)
        {
            return true;
        }

        return candidate.name.StartsWith(acceptedInputPrefab.name, StringComparison.Ordinal);
    }

    private Vector3 GetInputZoneCenter(Bounds localBounds)
    {
        Vector3 localCenter = localBounds.center;
        localCenter.z -= localBounds.extents.z + (inputZoneSize.z * 0.5f) - sideMargin;
        localCenter.y += holdHeight * 0.25f;
        return transform.TransformPoint(localCenter);
    }

    private Vector3 GetOutputZoneCenter(Bounds localBounds)
    {
        Vector3 localCenter = localBounds.center;
        localCenter.z += localBounds.extents.z + (outputZoneSize.z * 0.5f) - sideMargin + outputForwardOffset;
        localCenter.y += outputHeight * 0.25f;
        return transform.TransformPoint(localCenter);
    }

    private Vector3 GetHoldPosition(Bounds localBounds)
    {
        Vector3 localPoint = localBounds.center + Vector3.up * holdHeight;
        return transform.TransformPoint(localPoint);
    }

    private Vector3 GetOutputSpawnPosition(Bounds localBounds)
    {
        Vector3 localPoint = localBounds.center;
        localPoint.z += localBounds.extents.z + outputZoneSize.z + outputForwardOffset;
        localPoint.y += outputHeight;
        return transform.TransformPoint(localPoint);
    }

    private bool TryGetLocalBounds(out Bounds localBounds)
    {
        bool hasBounds = false;
        localBounds = default;

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider source = cachedColliders[i];
            if (source == null || !source.enabled || source.isTrigger)
            {
                continue;
            }

            EncapsulateBounds(source.bounds, ref hasBounds, ref localBounds);
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer source = cachedRenderers[i];
            if (source == null || !source.enabled)
            {
                continue;
            }

            EncapsulateBounds(source.bounds, ref hasBounds, ref localBounds);
        }

        return hasBounds;
    }

    private void EncapsulateBounds(Bounds worldBounds, ref bool hasBounds, ref Bounds localBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;

        Vector3[] points =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };

        for (int i = 0; i < points.Length; i++)
        {
            Vector3 localPoint = transform.InverseTransformPoint(points[i]);
            if (!hasBounds)
            {
                localBounds = new Bounds(localPoint, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                localBounds.Encapsulate(localPoint);
            }
        }
    }
}
