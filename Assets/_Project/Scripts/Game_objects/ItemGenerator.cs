using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Conveyor))]
public class ItemGenerator : MonoBehaviour
{
    private struct GeneratorSurface
    {
        public Vector3 Center;
        public Vector3 Extents;
        public Quaternion Rotation;
    }

    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private float spawnInterval = 1.5f;
    [SerializeField] private int maxItemsOnGenerator = 1;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.25f, 0f);

    private readonly Collider[] overlapResults = new Collider[32];
    private readonly HashSet<Rigidbody> detectedRigidbodies = new HashSet<Rigidbody>();
    private Collider[] cachedColliders = Array.Empty<Collider>();
    private Renderer[] cachedRenderers = Array.Empty<Renderer>();
    private float nextSpawnTime;

    private void Awake()
    {
        CacheColliders();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CacheColliders();
        }
    }

    private void FixedUpdate()
    {
        if (itemPrefab == null || (cachedColliders.Length == 0 && cachedRenderers.Length == 0))
        {
            return;
        }

        if (Time.time < nextSpawnTime)
        {
            return;
        }

        if (CountItemsOnGenerator() >= Mathf.Max(1, maxItemsOnGenerator))
        {
            return;
        }

        if (!TryGetPrimarySurface(out GeneratorSurface surface))
        {
            return;
        }

        Instantiate(itemPrefab, GetSpawnPosition(surface), Quaternion.identity);
        nextSpawnTime = Time.time + Mathf.Max(0.05f, spawnInterval);
    }

    private int CountItemsOnGenerator()
    {
        detectedRigidbodies.Clear();

        int surfaceCount = Mathf.Max(cachedColliders.Length, cachedColliders.Length > 0 ? 0 : 1);
        for (int i = 0; i < surfaceCount; i++)
        {
            if (!TryGetSurface(i, out GeneratorSurface surface))
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
                if (rb != null)
                {
                    detectedRigidbodies.Add(rb);
                }
            }
        }

        return detectedRigidbodies.Count;
    }

    private bool TryGetPrimarySurface(out GeneratorSurface surface)
    {
        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider generatorCollider = cachedColliders[i];
            if (generatorCollider != null && generatorCollider.enabled && !generatorCollider.isTrigger)
            {
                Bounds bounds = generatorCollider.bounds;
                surface = new GeneratorSurface
                {
                    Center = bounds.center,
                    Extents = bounds.extents,
                    Rotation = generatorCollider.transform.rotation
                };
                return true;
            }
        }

        return TryGetRendererSurface(out surface);
    }

    private Vector3 GetSpawnPosition(GeneratorSurface surface)
    {
        Vector3 worldOffset =
            transform.right * spawnOffset.x +
            Vector3.up * spawnOffset.y +
            transform.forward * spawnOffset.z;

        return surface.Center + Vector3.up * surface.Extents.y + worldOffset;
    }

    private void CacheColliders()
    {
        cachedColliders = GetComponentsInChildren<Collider>(includeInactive: false);
        cachedRenderers = GetComponentsInChildren<Renderer>(includeInactive: false);
    }

    private bool TryGetSurface(int index, out GeneratorSurface surface)
    {
        if (index < cachedColliders.Length)
        {
            Collider generatorCollider = cachedColliders[index];
            if (generatorCollider != null && generatorCollider.enabled && !generatorCollider.isTrigger)
            {
                Bounds bounds = generatorCollider.bounds;
                surface = new GeneratorSurface
                {
                    Center = bounds.center,
                    Extents = bounds.extents,
                    Rotation = generatorCollider.transform.rotation
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

    private bool TryGetRendererSurface(out GeneratorSurface surface)
    {
        Bounds? combinedBounds = null;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
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
        surface = new GeneratorSurface
        {
            Center = bounds.center,
            Extents = Vector3.Max(bounds.extents, Vector3.one * 0.05f),
            Rotation = transform.rotation
        };
        return true;
    }
}
