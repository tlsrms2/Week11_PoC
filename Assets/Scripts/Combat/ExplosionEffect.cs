using UnityEngine;

public class ExplosionEffect : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float fadeSpeed;
    private Color currentColor;

    public void Initialize(Sprite sprite, Color color, float targetDiameter, float duration = 0.3f)
    {
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        
        // 피격 이펙트가 너무 선명하여 덜 튀도록 알파 값을 10% 소폭 낮춤 (90% 투명도 유지)
        Color softColor = color;
        softColor.a *= 0.9f;
        
        spriteRenderer.color = softColor;
        spriteRenderer.sortingOrder = 10; // Ensure it renders above background/enemies
        
        // Calculate scale to ensure the sprite exactly matches the target diameter in world space
        float spriteWidth = sprite != null ? sprite.bounds.size.x : 1f;
        if (spriteWidth <= 0f) spriteWidth = 1f;
        
        float actualScale = targetDiameter / spriteWidth;
        transform.localScale = new Vector3(actualScale, actualScale, 1f);
        
        currentColor = softColor;
        // Calculate how much alpha to subtract per second
        fadeSpeed = softColor.a / duration;

        // Auto-destroy the effect after the duration
        Destroy(gameObject, duration);
    }

    private void Update()
    {
        if (spriteRenderer != null && currentColor.a > 0f)
        {
            currentColor.a -= fadeSpeed * Time.deltaTime;
            spriteRenderer.color = currentColor;
        }
    }
}
