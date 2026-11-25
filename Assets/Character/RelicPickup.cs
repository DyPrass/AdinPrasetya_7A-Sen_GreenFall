using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class RelicPickup : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 55f;
    [SerializeField] private float hoverHeight = 0.25f;
    [SerializeField] private float hoverSpeed = 1.8f;

    private GameManager gameManager;
    private Vector3 startPosition;
    private bool collected;

    public void SetGameManager(GameManager manager)
    {
        gameManager = manager;
    }

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        Vector3 position = startPosition;
        position.y += Mathf.Sin(Time.time * hoverSpeed) * hoverHeight;
        transform.position = position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
        {
            return;
        }

        PlayerCharacterController player = other.GetComponentInParent<PlayerCharacterController>();
        if (player == null)
        {
            return;
        }

        collected = true;
        gameManager?.CollectRelic();
        gameObject.SetActive(false);
    }
}
