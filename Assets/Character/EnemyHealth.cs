using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public sealed class EnemyHealth : MonoBehaviour
{
    [SerializeField] private float maximumHealth = 100f;
    [SerializeField] private Animator animator;
    [SerializeField] private HealthBar healthBar;
    [SerializeField] private bool destroyAfterDeath;
    [SerializeField] private float deathRemovalDelay = 3f;
    [SerializeField] private UnityEvent onDeath;

    public float CurrentHealth { get; private set; }
    public float MaximumHealth => maximumHealth;
    public bool IsDead { get; private set; }

    private void Awake()
    {
        CurrentHealth = maximumHealth;
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (healthBar == null)
        {
            healthBar = GetComponent<HealthBar>();
        }

        if (healthBar != null)
        {
            healthBar.SetTarget(this);
        }
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        if (CurrentHealth == 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        IsDead = true;
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.enabled = false;
        }

        EnemyCombat enemyCombat = GetComponent<EnemyCombat>();
        if (enemyCombat != null)
        {
            enemyCombat.enabled = false;
        }

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }

        if (animator != null)
        {
            animator.SetBool("IsChasing", false);
            animator.SetBool("IsAttacking", false);
            animator.SetBool("IsDead", true);
        }

        onDeath?.Invoke();
        if (destroyAfterDeath)
        {
            Destroy(gameObject, deathRemovalDelay);
        }
    }
}
