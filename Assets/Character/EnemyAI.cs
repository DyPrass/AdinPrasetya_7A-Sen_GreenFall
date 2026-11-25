using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CapsuleCollider))]
public sealed class EnemyAI : MonoBehaviour
{
    [SerializeField] private PlayerCharacterController targetPlayer;
    [SerializeField] private float detectionRadius = 18f;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float chaseSpeed = 3.5f;
    [SerializeField] private float turnSpeed = 540f;
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyCombat combat;

    private NavMeshAgent agent;
    private bool useNavMesh;

    public void SetTarget(PlayerCharacterController player)
    {
        targetPlayer = player;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (combat == null)
        {
            combat = GetComponent<EnemyCombat>();
        }
    }

    private void Start()
    {
        if (agent != null && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, agent.areaMask))
        {
            transform.position = hit.position;
            agent.enabled = true;
            useNavMesh = agent.isOnNavMesh;
            if (!useNavMesh)
            {
                agent.enabled = false;
            }
        }
    }

    private void Update()
    {
        if (targetPlayer == null || !targetPlayer.isActiveAndEnabled)
        {
            targetPlayer = FindAnyObjectByType<PlayerCharacterController>();
        }

        if (targetPlayer == null)
        {
            SetMoving(false);
            return;
        }

        Vector3 offset = targetPlayer.transform.position - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;
        if (distance > detectionRadius)
        {
            if (useNavMesh && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            SetMoving(false);
            return;
        }

        if (distance <= attackRange)
        {
            if (useNavMesh && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            SetMoving(false);
            combat?.TryAttack(targetPlayer);
            return;
        }

        if (distance <= Mathf.Epsilon)
        {
            SetMoving(false);
            return;
        }

        Vector3 direction = offset / distance;
        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);

        if (useNavMesh && agent.isOnNavMesh)
        {
            agent.speed = chaseSpeed;
            agent.SetDestination(targetPlayer.transform.position);
        }
        else
        {
            Vector3 nextPosition = transform.position + direction * chaseSpeed * Time.deltaTime;
            if (TryGetTerrainHeight(nextPosition, out float terrainHeight))
            {
                nextPosition.y = terrainHeight;
            }

            transform.position = nextPosition;
        }

        SetMoving(true);
    }

    private void SetMoving(bool moving)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetBool("IsChasing", moving);
        animator.SetFloat("Speed", moving ? 1f : 0f);
    }

    private bool TryGetTerrainHeight(Vector3 position, out float height)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (position.x >= origin.x && position.x <= origin.x + size.x &&
                position.z >= origin.z && position.z <= origin.z + size.z)
            {
                height = terrain.SampleHeight(position) + origin.y;
                return true;
            }
        }

        height = position.y;
        return false;
    }
}
