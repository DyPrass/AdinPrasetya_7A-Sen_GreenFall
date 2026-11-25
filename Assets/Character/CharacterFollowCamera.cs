using UnityEngine;
using UnityEngine.InputSystem;

public sealed class CharacterFollowCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -6.5f);
    [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private float followSpeed = 8f;
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minimumPitch = -20f;
    [SerializeField] private float maximumPitch = 65f;

    private PlayerCharacterController player;
    private float yaw;
    private float pitch = 12f;

    public void SetTarget(Transform followTarget)
    {
        target = followTarget;
        player = target != null ? target.GetComponent<PlayerCharacterController>() : null;
        if (target != null)
        {
            yaw = target.eulerAngles.y;
        }
    }

    public void SetMouseSensitivity(float value)
    {
        mouseSensitivity = Mathf.Clamp(value, 0.04f, 0.4f);
    }

    private void Start()
    {
        if (target == null)
        {
            return;
        }

        player = target.GetComponent<PlayerCharacterController>();
        yaw = target.eulerAngles.y;
    }

    private void Update()
    {
        if (target == null || player == null || !player.isActiveAndEnabled || Mouse.current == null)
        {
            return;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        yaw += mouseDelta.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - mouseDelta.y * mouseSensitivity, minimumPitch, maximumPitch);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 focusPosition = target.position + lookOffset;
        Vector3 cameraOffset = orbitRotation * new Vector3(offset.x, offset.y - lookOffset.y, offset.z);
        Vector3 desiredPosition = focusPosition + cameraOffset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
        transform.LookAt(focusPosition);
    }
}
