using System;
using System.Collections.Generic;
using UnityEngine;

public class BuildingCatalog : MonoBehaviour
{
    [Serializable]
    public class BuildingEntry
    {
        [Min(1)]
        public int typeId = 1;
        public GameObject buildingPrefab;
    }

    [SerializeField] private List<BuildingEntry> entries = new List<BuildingEntry>();

    private readonly Dictionary<int, BuildingEntry> entriesByTypeId = new Dictionary<int, BuildingEntry>();

    private void Awake()
    {
        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    public bool TryGetEntry(int typeId, out BuildingEntry entry)
    {
        RebuildLookup();
        return entriesByTypeId.TryGetValue(typeId, out entry);
    }

    private void RebuildLookup()
    {
        entriesByTypeId.Clear();

        for (int i = 0; i < entries.Count; i++)
        {
            BuildingEntry entry = entries[i];
            if (entry == null)
            {
                continue;
            }

            if (entriesByTypeId.ContainsKey(entry.typeId))
            {
                Debug.LogWarning($"BuildingCatalog '{name}' contains duplicate typeId {entry.typeId}. Keeping the first entry.");
                continue;
            }

            entriesByTypeId.Add(entry.typeId, entry);
        }
    }
}
