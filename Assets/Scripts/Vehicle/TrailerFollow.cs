using UnityEngine;

public class TrailerFollow : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private Transform target;
    [SerializeField] private float followDistance = 0f; // Default anchor-to-anchor distance to 0f for perfect overlapping hinge pins
    [SerializeField] private float followSpeed = 8f;
    [SerializeField] private bool followOnlyDuringDefense = false; // Changed default to false for constant train-like movement
    [SerializeField] private float maxHingeAngle = 75f; // Limits relative angle to prevent truck-trailer penetration

    private TruckBody targetTruck;
    private TrailerBody targetTrailer;
    private TrailerBody myBody;
    private Transform lastTarget;

    // Virtual tractrix tracking to prevent feedback locks when using rotation smoothing
    private Vector3 virtualPosition;
    private bool isVirtualPositionInitialized = false;

    public Transform Target => target;

    private void Start()
    {
        myBody = GetComponent<TrailerBody>();
        followOnlyDuringDefense = false; // Hard-enforce false to bypass serialized true values and maintain continuous follow logic
        AlignInstantly();
    }

    private void OnValidate()
    {
        followDistance = Mathf.Max(0f, followDistance);
        followSpeed = Mathf.Max(0.1f, followSpeed);
        maxHingeAngle = Mathf.Clamp(maxHingeAngle, 10f, 170f);
    }

    private void CacheTargetComponents()
    {
        if (target == lastTarget) return;

        lastTarget = target;
        if (target != null)
        {
            targetTruck = target.GetComponent<TruckBody>();
            targetTrailer = target.GetComponent<TrailerBody>();
        }
        else
        {
            targetTruck = null;
            targetTrailer = null;
        }
    }

    private void Update()
    {
        if (target == null)
        {
            return;
        }

        if (followOnlyDuringDefense && !GamePhaseManager.IsDefense)
        {
            return;
        }

        CacheTargetComponents();

        // 1. Determine if we should use steering/physics tracking
        bool useSteering = false;
        if (targetTruck != null)
        {
            TruckMovement tm = targetTruck.GetComponent<TruckMovement>();
            if (tm != null) useSteering = tm.UseCarSteering;
        }
        else if (targetTrailer != null)
        {
            // Traverse up to find the lead truck's steering setting dynamically
            TruckMovement tm = TruckBody.Instance != null ? TruckBody.Instance.GetComponent<TruckMovement>() : null;
            if (tm != null) useSteering = tm.UseCarSteering;
        }

        if (!useSteering)
        {
            // 2. Simple 2D Translation alignment (No Rotation)
            transform.rotation = Quaternion.identity; // Force straight orientation facing right

            Vector2 targetRearAnchorOffset = new Vector2(-1.5f, 0f);
            if (targetTruck != null)
            {
                targetRearAnchorOffset = targetTruck.RearAnchorOffset;
            }
            else if (targetTrailer != null)
            {
                targetRearAnchorOffset = targetTrailer.RearAnchorOffset;
            }

            Vector2 myFrontAnchorOffset = new Vector2(1.5f, 0f);
            if (myBody == null)
            {
                myBody = GetComponent<TrailerBody>();
            }
            if (myBody != null)
            {
                myFrontAnchorOffset = myBody.FrontAnchorOffset;
            }

            Vector3 worldRearAnchor = target.TransformPoint(targetRearAnchorOffset);
            Vector3 worldFrontOffset = transform.TransformVector(myFrontAnchorOffset);
            
            // Snap positioning perfectly along the line
            transform.position = worldRearAnchor - worldFrontOffset;

            // Sync virtual tracking state to prevent dynamic physics snap jumps later
            virtualPosition = transform.position;
            isVirtualPositionInitialized = true;
            return;
        }

        // 3. Existing Tractrix and Hinge-Joint Physics Mode
        if (!isVirtualPositionInitialized)
        {
            virtualPosition = transform.position;
            isVirtualPositionInitialized = true;
        }

        Vector2 targetRearAnchorOffsetPhysics = new Vector2(-1.5f, 0f);
        if (targetTruck != null)
        {
            targetRearAnchorOffsetPhysics = targetTruck.RearAnchorOffset;
        }
        else if (targetTrailer != null)
        {
            targetRearAnchorOffsetPhysics = targetTrailer.RearAnchorOffset;
        }

        Vector2 myFrontAnchorOffsetPhysics = new Vector2(1.5f, 0f);
        if (myBody == null)
        {
            myBody = GetComponent<TrailerBody>();
        }
        if (myBody != null)
        {
            myFrontAnchorOffsetPhysics = myBody.FrontAnchorOffset;
        }

        Vector3 worldRearAnchorPhysics = target.TransformPoint(targetRearAnchorOffsetPhysics);

        Vector3 worldFrontAnchorOffsetPhysics = transform.TransformVector(myFrontAnchorOffsetPhysics);
        float L = worldFrontAnchorOffsetPhysics.magnitude;

        Vector3 dir = virtualPosition - worldRearAnchorPhysics;
        float dist = dir.magnitude;

        if (dist <= 0.0001f)
        {
            dir = -target.right;
        }

        virtualPosition = worldRearAnchorPhysics + dir.normalized * L;

        Vector3 heading = worldRearAnchorPhysics - virtualPosition;
        float targetAngle = Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg;

        float targetFacingAngle = target.eulerAngles.z;
        float relativeAngle = Mathf.DeltaAngle(targetFacingAngle, targetAngle);
        relativeAngle = Mathf.Clamp(relativeAngle, -maxHingeAngle, maxHingeAngle);
        targetAngle = targetFacingAngle + relativeAngle;

        float currentAngle = transform.eulerAngles.z;
        float smoothAngle = Mathf.LerpAngle(currentAngle, targetAngle, followSpeed * Time.deltaTime);

        float proposedRelativeAngle = Mathf.DeltaAngle(targetFacingAngle, smoothAngle);
        float absProposedRelative = Mathf.Abs(proposedRelativeAngle);
        float sign = Mathf.Sign(proposedRelativeAngle);

        Collider2D targetCollider = null;
        if (targetTruck != null) targetCollider = targetTruck.BodyCollider;
        else if (targetTrailer != null) targetCollider = targetTrailer.BodyCollider;

        float finalRelativeAngle = proposedRelativeAngle;
        if (targetCollider != null && myBody != null && myBody.BodyCollider != null)
        {
            if (TestOverlapAtAngle(proposedRelativeAngle, targetFacingAngle, worldRearAnchorPhysics, myFrontAnchorOffsetPhysics, targetCollider))
            {
                float low = 0f;
                float high = absProposedRelative;
                float safeAbsRelative = 0f;

                for (int i = 0; i < 8; i++)
                {
                    float mid = (low + high) * 0.5f;
                    float testAngle = mid * sign;
                    if (!TestOverlapAtAngle(testAngle, targetFacingAngle, worldRearAnchorPhysics, myFrontAnchorOffsetPhysics, targetCollider))
                    {
                        safeAbsRelative = mid;
                        low = mid;
                    }
                    else
                    {
                        high = mid;
                    }
                }
                finalRelativeAngle = safeAbsRelative * sign;
            }
        }

        float finalAngle = targetFacingAngle + finalRelativeAngle;
        transform.rotation = Quaternion.Euler(0, 0, finalAngle);

        Vector3 finalWorldFrontOffset = transform.TransformVector(myFrontAnchorOffsetPhysics);
        transform.position = worldRearAnchorPhysics - finalWorldFrontOffset;
    }

    public void Configure(Transform nextTarget, float distance, float speed)
    {
        target = nextTarget;
        // Spacing calibration: force anchor overlapping by setting followDistance to 0f
        followDistance = 0f; 
        followSpeed = Mathf.Max(0.1f, speed);
        followOnlyDuringDefense = false; // Hard-enforce continuous steering tracking
        CacheTargetComponents();
        AlignInstantly();
    }

    [ContextMenu("Align Instantly")]
    public void AlignInstantly()
    {
        if (target == null) return;
        if (myBody == null) myBody = GetComponent<TrailerBody>();
        CacheTargetComponents();

        Vector2 targetRearAnchorOffset = new Vector2(-1.5f, 0f);
        if (targetTruck != null)
        {
            targetRearAnchorOffset = targetTruck.RearAnchorOffset;
        }
        else if (targetTrailer != null)
        {
            targetRearAnchorOffset = targetTrailer.RearAnchorOffset;
        }

        Vector2 myFrontAnchorOffset = new Vector2(1.5f, 0f);
        if (myBody != null)
        {
            myFrontAnchorOffset = myBody.FrontAnchorOffset;
        }

        Vector3 worldRearAnchor = target.TransformPoint(targetRearAnchorOffset);

        // Snap rotation to target's rotation initially
        transform.rotation = target.rotation;

        Vector3 desiredFrontAnchor = worldRearAnchor - transform.rotation * (Vector3.right * followDistance);
        
        // Use TransformVector to correctly factor in our localScale/lossyScale into the world front anchor offset vector
        Vector3 worldFrontAnchorOffset = transform.TransformVector(myFrontAnchorOffset);
        transform.position = desiredFrontAnchor - worldFrontAnchorOffset;

        // Synchronize virtualPosition with snapped position
        virtualPosition = transform.position;
        isVirtualPositionInitialized = true;
    }

    private bool TestOverlapAtAngle(float relAngle, float targetFacingAngle, Vector3 worldRearAnchor, Vector3 myFrontAnchorOffset, Collider2D targetCollider)
    {
        float testAngle = targetFacingAngle + relAngle;
        transform.rotation = Quaternion.Euler(0, 0, testAngle);
        
        Vector3 worldFrontOffset = transform.TransformVector(myFrontAnchorOffset);
        transform.position = worldRearAnchor - worldFrontOffset;

        Physics2D.SyncTransforms();

        if (myBody != null && myBody.BodyCollider != null && targetCollider != null)
        {
            ColliderDistance2D dist = myBody.BodyCollider.Distance(targetCollider);
            if (dist.isValid)
            {
                return dist.isOverlapped;
            }
        }
        return false;
    }
}
