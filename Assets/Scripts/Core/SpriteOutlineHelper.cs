using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOutlineHelper : MonoBehaviour
{
    public Color outlineColor = Color.black;
    [Range(0f, 0.2f)] public float outlineThickness = 0.03f;

    private SpriteRenderer mainRenderer;
    private SpriteRenderer[] outlineRenderers = new SpriteRenderer[4];
    private bool isInitialized = false;

    private void Start()
    {
        InitializeOutline();
    }

    private void InitializeOutline()
    {
        if (isInitialized) return;
        mainRenderer = GetComponent<SpriteRenderer>();

        Vector3[] offsets = new Vector3[4]
        {
            new Vector3(0, outlineThickness, 0),
            new Vector3(0, -outlineThickness, 0),
            new Vector3(-outlineThickness, 0, 0),
            new Vector3(outlineThickness, 0, 0)
        };

        for (int i = 0; i < 4; i++)
        {
            GameObject outlineObj = new GameObject("OutlineChild_" + i);
            outlineObj.transform.SetParent(transform);
            outlineObj.transform.localPosition = offsets[i];
            outlineObj.transform.localRotation = Quaternion.identity;
            outlineObj.transform.localScale = Vector3.one;

            SpriteRenderer sr = outlineObj.AddComponent<SpriteRenderer>();
            sr.sprite = mainRenderer.sprite;
            sr.color = outlineColor;
            sr.sortingLayerID = mainRenderer.sortingLayerID;
            sr.sortingOrder = mainRenderer.sortingOrder - 1;
            
            // Set material to a simple unlit sprite material if possible, or just use the same
            sr.material = mainRenderer.material; 
            
            outlineRenderers[i] = sr;
        }

        isInitialized = true;
    }

    private void LateUpdate()
    {
        if (!isInitialized || mainRenderer == null) return;
        
        // Sync sprite if it changes (e.g. animation)
        for (int i = 0; i < 4; i++)
        {
            if (outlineRenderers[i] != null)
            {
                outlineRenderers[i].sprite = mainRenderer.sprite;
                outlineRenderers[i].flipX = mainRenderer.flipX;
                outlineRenderers[i].flipY = mainRenderer.flipY;
            }
        }
    }
}
