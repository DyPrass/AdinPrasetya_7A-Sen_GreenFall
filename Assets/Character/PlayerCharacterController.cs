using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class PlayerCharacterController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float jumpHeight = 1.4f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float maximumHealth = 100f;
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackCooldown = 0.8f;
    [SerializeField] private float attackHitDelay = 0.25f;
    [SerializeField] private float attackAnimationDuration = 0.8f;

    private CharacterController characterController;
    private float currentHealth;
    private float verticalSpeed;
    private float nextAttackTime;
    private bool isDead;

    public float CurrentHealth => currentHealth;
    public float MaximumHealth => maximumHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        currentHealth = maximumHealth;
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        UpdateMovement();

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if ((keyboard != null && keyboard.leftCtrlKey.wasPressedThisFrame) ||
            (mouse != null && mouse.leftButton.wasPressedThisFrame))
        {
            TryAttack();
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (currentHealth == 0f)
        {
            isDead = true;
            if (animator != null)
            {
                animator.SetBool("IsChasing", false);
                animator.SetBool("IsAttacking", false);
                animator.SetBool("IsDead", true);
            }

            characterController.enabled = false;
        }
    }

    private void UpdateMovement()
    {
        Keyboard keyboard = Keyboard.current;
        Vector2 input = Vector2.zero;
        if (keyboard != null)
        {
            input.x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            input.y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
        }

        input = Vector2.ClampMagnitude(input, 1f);
        Transform cameraTransform = Camera.main != null ? Camera.main.transform : transform;
        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 moveDirection = (forward * input.y + right * input.x).normalized;
        bool isMoving = moveDirection.sqrMagnitude > 0.001f;
        bool isRunning = keyboard != null && keyboard.leftShiftKey.isPressed;

        if (isMoving)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        bool isGrounded = characterController.isGrounded;
        if (isGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = -2f;
        }
        else
        {
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
        }

        if (isGrounded && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
        }

        float speed = isRunning ? runSpeed : walkSpeed;
        characterController.Move((moveDirection * speed + Vector3.up * verticalSpeed) * Time.deltaTime);

        if (animator != null)
        {
            animator.SetBool("IsChasing", isMoving);
            animator.SetFloat("Speed", isRunning && isMoving ? 1f : 0.5f);
        }
    }

    private void TryAttack()
    {
        if (Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + attackCooldown;
        if (animator != null)
        {
            animator.SetBool("IsAttacking", true);
        }

        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(attackHitDelay);

        EnemyHealth nearestEnemy = null;
        float nearestDistance = attackRange;
        foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>())
        {
            if (enemy == null || enemy.IsDead)
            {
                continue;
            }

            Vector3 directionToEnemy = enemy.transform.position - transform.position;
            directionToEnemy.y = 0f;
            float distance = directionToEnemy.magnitude;
            if (distance > 0f && Vector3.Dot(transform.forward, directionToEnemy / distance) < 0.15f)
            {
                continue;
            }

            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearestEnemy = enemy;
            }
        }

        if (nearestEnemy != null)
        {
            nearestEnemy.TakeDamage(attackDamage);
        }

        yield return new WaitForSeconds(Mathf.Max(0f, attackAnimationDuration - attackHitDelay));
        if (!isDead && animator != null)
        {
            animator.SetBool("IsAttacking", false);
        }
    }
}
