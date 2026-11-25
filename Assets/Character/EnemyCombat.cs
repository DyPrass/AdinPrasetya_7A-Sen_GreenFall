using System.Collections;
using UnityEngine;

public sealed class EnemyCombat : MonoBehaviour
{
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float hitTime = 0.35f;
    [SerializeField] private float attackAnimationDuration = 1f;
    [SerializeField] private Animator animator;

    private float nextAttackTime;
    private bool isAttacking;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    public void TryAttack(PlayerCharacterController target)
    {
        if (target == null || isAttacking || Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + attackCooldown;
        StartCoroutine(AttackRoutine(target));
    }

    private IEnumerator AttackRoutine(PlayerCharacterController target)
    {
        isAttacking = true;
        if (animator != null)
        {
            animator.SetBool("IsAttacking", true);
        }

        yield return new WaitForSeconds(hitTime);
        if (target != null && Vector3.Distance(transform.position, target.transform.position) <= 2.2f)
        {
            target.TakeDamage(attackDamage);
        }

        yield return new WaitForSeconds(Mathf.Max(0f, attackAnimationDuration - hitTime));
        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
        }

        isAttacking = false;
    }
}
