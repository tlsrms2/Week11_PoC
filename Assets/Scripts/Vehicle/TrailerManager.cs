using System.Collections.Generic;
using UnityEngine;

public class TrailerManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TruckBody truckBody;
    [SerializeField] private CurrencyWallet currencyWallet;
    [SerializeField] private TrailerBody trailerPrefab;
    [SerializeField] private Transform trailerParent;

    [Header("Expansion")]
    [SerializeField] private int maxTrailers = 3;
    [SerializeField] private int expansionCost = 25;
    [SerializeField] private float trailerSpacing = 1.8f;
    [SerializeField] private float trailerFollowSpeed = 8f;

    private readonly List<TrailerBody> trailers = new List<TrailerBody>();
    private float lastExpansionTime = -1f;

    public int TrailerCount => CountActiveTrailers();
    public int MaxTrailers => maxTrailers;
    public int ExpansionCost => expansionCost;
    public bool CanAddTrailer => TrailerCount < maxTrailers
        && GamePhaseManager.IsMaintenance
        && (currencyWallet == null || currencyWallet.CurrentCurrency >= expansionCost)
        && Time.time > lastExpansionTime + 0.5f; // 0.5s cooldown

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        maxTrailers = Mathf.Max(0, maxTrailers);
        expansionCost = Mathf.Max(0, expansionCost);
        trailerSpacing = Mathf.Max(0.1f, trailerSpacing);
        trailerFollowSpeed = Mathf.Max(0.1f, trailerFollowSpeed);
    }

    public bool TryAddTrailer()
    {
        FindReferences();

        if (!CanAddTrailer)
        {
            return false;
        }

        if (currencyWallet != null && !currencyWallet.TrySpend(expansionCost))
        {
            return false;
        }

        lastExpansionTime = Time.time;
        TrailerBody trailer = CreateTrailer();
        trailers.Add(trailer);
        Debug.Log($"Added trailer. Current count: {trailers.Count}");
        return true;
    }

    private TrailerBody CreateTrailer()
    {
        Transform followTarget = GetLastFollowTarget();
        
        // Calculate anchors for precise spawning alignment (no gaps)
        Vector2 targetRearAnchorOffset = new Vector2(-1.5f, 0f);
        if (followTarget != null)
        {
            TruckBody targetTruck = followTarget.GetComponent<TruckBody>();
            TrailerBody targetTrailer = followTarget.GetComponent<TrailerBody>();
            if (targetTruck != null)
            {
                targetRearAnchorOffset = targetTruck.RearAnchorOffset;
            }
            else if (targetTrailer != null)
            {
                targetRearAnchorOffset = targetTrailer.RearAnchorOffset;
            }
        }

        Vector2 myFrontAnchorOffset = new Vector2(1.5f, 0f);
        if (trailerPrefab != null)
        {
            myFrontAnchorOffset = trailerPrefab.FrontAnchorOffset;
        }

        // Get target's world rear anchor
        Vector3 targetRearAnchorWorld = followTarget != null
            ? followTarget.TransformPoint(targetRearAnchorOffset)
            : transform.position;

        // Snap rotation to target initially
        Quaternion spawnRotation = followTarget != null ? followTarget.rotation : Quaternion.identity;

        // Transform local front anchor to world offset using prefab's local scale
        Vector3 myScale = trailerPrefab != null ? trailerPrefab.transform.localScale : Vector3.one;
        Vector3 myFrontAnchorWorldOffset = spawnRotation * Vector3.Scale(myFrontAnchorOffset, myScale);

        // Ultimate exact overlapping spawn position
        Vector3 spawnPosition = targetRearAnchorWorld - myFrontAnchorWorldOffset;

        TrailerBody trailer;
        if (trailerPrefab != null)
        {
            // 1. Spawns under trailerParent to keep hierarchy organized
            trailer = Instantiate(trailerPrefab, spawnPosition, spawnRotation, trailerParent);
            
            // 2. Divide prefab's scale by parent's lossyScale to ensure trailer's final world scale exactly matches the prefab
            if (trailerParent != null)
            {
                Vector3 parentLossy = trailerParent.lossyScale;
                Vector3 prefabLocal = trailerPrefab.transform.localScale;
                
                float lx = Mathf.Approximately(parentLossy.x, 0f) ? 1f : prefabLocal.x / parentLossy.x;
                float ly = Mathf.Approximately(parentLossy.y, 0f) ? 1f : prefabLocal.y / parentLossy.y;
                float lz = Mathf.Approximately(parentLossy.z, 0f) ? 1f : prefabLocal.z / parentLossy.z;
                
                trailer.transform.localScale = new Vector3(lx, ly, lz);
            }
            else
            {
                trailer.transform.localScale = trailerPrefab.transform.localScale;
            }
        }
        else
        {
            GameObject trailerObject = new GameObject($"Trailer_{trailers.Count + 1:00}");
            if (trailerParent != null)
            {
                trailerObject.transform.SetParent(trailerParent);
            }
            trailerObject.transform.position = spawnPosition;
            trailerObject.transform.rotation = spawnRotation;
            
            if (trailerParent != null)
            {
                Vector3 parentLossy = trailerParent.lossyScale;
                
                float lx = Mathf.Approximately(parentLossy.x, 0f) ? 1f : 1f / parentLossy.x;
                float ly = Mathf.Approximately(parentLossy.y, 0f) ? 1f : 1f / parentLossy.y;
                float lz = Mathf.Approximately(parentLossy.z, 0f) ? 1f : 1f / parentLossy.z;
                
                trailerObject.transform.localScale = new Vector3(lx, ly, lz);
            }
            else
            {
                trailerObject.transform.localScale = Vector3.one;
            }

            GameObject gridObject = new GameObject("TrailerGrid");
            gridObject.transform.SetParent(trailerObject.transform, false);
            gridObject.AddComponent<TruckGrid>();

            trailer = trailerObject.AddComponent<TrailerBody>();
        }

        TrailerFollow follow = trailer.GetComponent<TrailerFollow>();
        if (follow == null)
        {
            follow = trailer.gameObject.AddComponent<TrailerFollow>();
        }

        follow.Configure(followTarget, trailerSpacing, trailerFollowSpeed);
        return trailer;
    }

    private Transform GetLastFollowTarget()
    {
        for (int i = trailers.Count - 1; i >= 0; i--)
        {
            if (trailers[i] != null && trailers[i].isActiveAndEnabled)
            {
                return trailers[i].transform;
            }
        }

        return truckBody != null ? truckBody.transform : transform;
    }

    private int CountActiveTrailers()
    {
        int count = 0;
        for (int i = trailers.Count - 1; i >= 0; i--)
        {
            if (trailers[i] == null)
            {
                trailers.RemoveAt(i);
                continue;
            }

            if (trailers[i].isActiveAndEnabled)
            {
                count++;
            }
        }

        return count;
    }

    private void FindReferences()
    {
        if (truckBody == null)
        {
            truckBody = FindFirstObjectByType<TruckBody>();
        }

        if (currencyWallet == null)
        {
            currencyWallet = CurrencyWallet.Instance;
        }
    }
}
