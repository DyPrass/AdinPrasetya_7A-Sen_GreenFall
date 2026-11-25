using UnityEngine;

public sealed class HealthBar : MonoBehaviour
{
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.4f, 0f);
    [SerializeField] private float visibleDistance = 18f;
    [SerializeField] private Color healthyColor = new Color(0.15f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color criticalColor = new Color(0.9f, 0.12f, 0.08f, 1f);

    private EnemyHealth target;

    public void SetTarget(EnemyHealth health)
    {
        target = health;
    }

    private void OnGUI()
    {
        Camera camera = Camera.main;
        if (camera == null || target == null || target.IsDead ||
            Vector3.Distance(camera.transform.position, target.transform.position) > visibleDistance)
        {
            return;
        }

        Vector3 screenPoint = camera.WorldToScreenPoint(target.transform.position + worldOffset);
        if (screenPoint.z <= 0f)
        {
            return;
        }

        Rect background = new Rect(screenPoint.x - 28f, Screen.height - screenPoint.y, 56f, 7f);
        float healthRatio = target.MaximumHealth <= 0f ? 0f : target.CurrentHealth / target.MaximumHealth;
        GUI.color = Color.black;
        GUI.DrawTexture(background, Texture2D.whiteTexture);
        GUI.color = Color.Lerp(criticalColor, healthyColor, healthRatio);
        GUI.DrawTexture(new Rect(background.x + 1f, background.y + 1f, (background.width - 2f) * healthRatio, background.height - 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
