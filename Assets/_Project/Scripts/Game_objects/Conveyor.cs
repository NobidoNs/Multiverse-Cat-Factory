using System;
using System.Collections.Generic;
using UnityEngine;

public class Conveyor : MonoBehaviour
{
    private struct ConveyorSurface
    {
        public Vector3 Center;
        public Vector3 Extents;
        public Quaternion Rotation;
    }

    public float speed = 5f;
    public Material mt;
    public float textureScrollSpeed = 1f;

    private readonly Collider[] overlapResults = new Collider[32];
    private readonly HashSet<Rigidbody> processedRigidbodies = new HashSet<Rigidbody>();
    private Collider[] conveyorColliders = Array.Empty<Collider>();
    private Renderer[] conveyorRenderers = Array.Empty<Renderer>();

    private void Awake()
    {
        CacheSurfaces();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CacheSurfaces();
        }
    }

    private void FixedUpdate()
    {
        if (mt != null)
        {
            float offset = Time.time * textureScrollSpeed;
            mt.mainTextureOffset = new Vector2(offset, 0f);
        }

        PushObjectsOnBelt();
    }

    private void PushObjectsOnBelt()
    {
        bool hasColliders = conveyorColliders.Length > 0;
        bool hasRenderers = conveyorRenderers.Length > 0;
        if (!hasColliders && !hasRenderers)
        {
            return;
        }

        processedRigidbodies.Clear();

        Vector3 pushDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (pushDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            pushDirection = transform.forward.normalized;
        }

        int surfaceCount = Mathf.Max(conveyorColliders.Length, hasColliders ? 0 : 1);
        for (int i = 0; i < surfaceCount; i++)
        {
            if (!TryGetSurface(i, out ConveyorSurface surface))
            {
                continue;
            }

            int hitCount = Physics.OverlapBoxNonAlloc(
                surface.Center,
                surface.Extents,
                overlapResults,
                surface.Rotation,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider hitCollider = overlapResults[hitIndex];
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                Rigidbody rb = hitCollider.attachedRigidbody;
                if (rb == null || rb.isKinematic || !processedRigidbodies.Add(rb))
                {
                    continue;
                }

                rb.MovePosition(rb.position + pushDirection * (speed * Time.fixedDeltaTime));
            }
        }
    }

    private void CacheColliders()
    {
        conveyorColliders = GetComponentsInChildren<Collider>(includeInactive: false);
    }

    private void CacheSurfaces()
    {
        CacheColliders();
        conveyorRenderers = GetComponentsInChildren<Renderer>(includeInactive: false);
    }

    private bool TryGetSurface(int index, out ConveyorSurface surface)
    {
        if (index < conveyorColliders.Length)
        {
            Collider conveyorCollider = conveyorColliders[index];
            if (conveyorCollider != null && conveyorCollider.enabled && !conveyorCollider.isTrigger)
            {
                Bounds bounds = conveyorCollider.bounds;
                surface = new ConveyorSurface
                {
                    Center = bounds.center,
                    Extents = bounds.extents,
                    Rotation = conveyorCollider.transform.rotation
                };
                return true;
            }
        }

        if (TryGetRendererSurface(out surface))
        {
            return true;
        }

        surface = default;
        return false;
    }

    private bool TryGetRendererSurface(out ConveyorSurface surface)
    {
        Bounds? combinedBounds = null;

        for (int i = 0; i < conveyorRenderers.Length; i++)
        {
            Renderer renderer = conveyorRenderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (combinedBounds.HasValue)
            {
                Bounds value = combinedBounds.Value;
                value.Encapsulate(renderer.bounds);
                combinedBounds = value;
            }
            else
            {
                combinedBounds = renderer.bounds;
            }
        }

        if (!combinedBounds.HasValue)
        {
            surface = default;
            return false;
        }

        Bounds bounds = combinedBounds.Value;
        surface = new ConveyorSurface
        {
            Center = bounds.center,
            Extents = Vector3.Max(bounds.extents, Vector3.one * 0.05f),
            Rotation = transform.rotation
        };
        return true;
    }
}
