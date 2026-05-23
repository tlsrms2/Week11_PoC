using UnityEngine;
using TMPro;

/// <summary>
/// 적 피격 시 월드 스페이스 상에 동적으로 생성되어 데미지를 표기해주는 프리미엄 팝업 스크립트입니다.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class DamageTextPopup : MonoBehaviour
{
    private TextMeshPro textMesh;
    private Color32 topColor;
    private Color32 bottomColor;
    private float fadeTimer;
    private float alpha = 1f;

    // Material for outline (to prevent memory leaks, we clean this up in OnDestroy)
    private Material clonedMaterial;

    // Movement & Physics
    private float moveSpeedY;
    private float moveSpeedX;
    private float fadeSpeed = 3.5f;
    private float scaleSpeed = 12f;
    private Vector3 targetScale;

    /// <summary>
    /// 지정된 위치에 데미지 수치 팝업을 생성합니다.
    /// </summary>
    public static void Create(Vector3 position, float damageAmount, Color customColor = default)
    {
        GameObject popupObj = new GameObject("DamageTextPopup");
        
        // 피격 위치에 약간의 무작위 분산을 주어 연속 타격 시 텍스트가 겹치지 않게 합니다.
        float randomX = Random.Range(-0.35f, 0.35f);
        float randomY = Random.Range(0.2f, 0.45f);
        popupObj.transform.position = position + new Vector3(randomX, randomY, 0f);

        DamageTextPopup popup = popupObj.AddComponent<DamageTextPopup>();
        
        // 임계값 15를 초과하는 강력한 공격이거나 15%의 확률로 크리티컬 연출을 부여합니다.
        bool isCritical = (damageAmount >= 15f) || (Random.value < 0.15f);
        popup.Setup(damageAmount, isCritical, customColor);
    }

    private void Setup(float damageAmount, bool isCritical, Color customColor)
    {
        // TextMeshPro 컴포넌트 추가
        textMesh = gameObject.AddComponent<TextMeshPro>();

        // 씬 내에 로드된 폰트 중 'Paperlogy' 커스텀 폰트가 있는지 검색 및 자동 적용
        TMP_FontAsset customFont = null;
        TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (var font in loadedFonts)
        {
            if (font != null && font.name.Contains("Paperlogy"))
            {
                customFont = font;
                break;
            }
        }

        if (customFont != null)
        {
            textMesh.font = customFont;
        }

        // 데미지 수치 반올림
        textMesh.text = Mathf.RoundToInt(damageAmount).ToString();
        textMesh.alignment = TextAlignmentOptions.Center;
        
        // 폰트 스타일 굵게 적용
        textMesh.fontStyle = FontStyles.Bold;

        // 버텍스 그라데이션을 적용하여 고급스럽고 입체감 넘치는 텍스트 렌더링
        textMesh.enableVertexGradient = true;

        // 폰트 크기 설정: 최대 4.7까지만 커지도록 크리티컬 기준 4.7f, 일반 기준 3.5f 설정
        if (isCritical)
        {
            textMesh.fontSize = 4.7f;
            
            // 크리티컬 팝업 시 초기 크기를 0으로 두고 크게 팝하도록 목표 설정
            transform.localScale = Vector3.zero;
            targetScale = new Vector3(1.2f, 1.2f, 1.2f);
            
            // 더 크고 넓게 튕겨 올라가는 시각적 역동성
            moveSpeedY = 2.1f;
            moveSpeedX = Random.Range(-0.7f, 0.7f);
        }
        else
        {
            textMesh.fontSize = 3.5f;
            
            // 일반 팝업 시 초기 크기 0에서 시작
            transform.localScale = Vector3.zero;
            targetScale = Vector3.one;
            
            // 스탠다드한 상승 모션
            moveSpeedY = 1.3f;
            moveSpeedX = Random.Range(-0.3f, 0.3f);
        }

        // 색상 지정: 투사체 폭발 이펙트 색상이 전달된 경우 해당 색상을 적극 반영
        // 그라데이션을 통해 3D 입체감을 살릴 수 있도록 밑부분은 45% 정도 어두운 톤으로 자동 매칭 처리
        if (customColor != default(Color) && customColor.a > 0.05f)
        {
            topColor = new Color32(
                (byte)Mathf.Clamp(customColor.r * 255f, 0f, 255f),
                (byte)Mathf.Clamp(customColor.g * 255f, 0f, 255f),
                (byte)Mathf.Clamp(customColor.b * 255f, 0f, 255f),
                255
            );
            
            float darkFactor = 0.55f; // 밑부분 그림자 효과 계수
            bottomColor = new Color32(
                (byte)Mathf.Clamp(customColor.r * 255f * darkFactor, 0f, 255f),
                (byte)Mathf.Clamp(customColor.g * 255f * darkFactor, 0f, 255f),
                (byte)Mathf.Clamp(customColor.b * 255f * darkFactor, 0f, 255f),
                255
            );
        }
        else
        {
            // 투사체 색상이 없는 경우의 고품질 기본 그라데이션 폴백
            if (isCritical)
            {
                topColor = new Color32(255, 30, 90, 255);      // Neon Pink
                bottomColor = new Color32(180, 0, 40, 255);    // Deep Crimson
            }
            else
            {
                topColor = new Color32(255, 220, 10, 255);     // Gold Yellow
                bottomColor = new Color32(255, 100, 0, 255);    // Warm Orange
            }
        }

        UpdateTextColor();

        // 렌더링 정렬 설정 (몬스터 본체, 체력바, 이펙트보다 항상 최상위에 오도록 정렬 순서 조정)
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder = 50;
        }

        // 폰트 아웃라인(테두리) 활성화 및 입체감 극대화 설정
        clonedMaterial = textMesh.fontMaterial;
        if (clonedMaterial != null)
        {
            clonedMaterial.EnableKeyword("OUTLINE_ON");
            clonedMaterial.SetColor("_OutlineColor", new Color32(0, 0, 0, 230)); // 90% 불투명한 짙은 검은색 아웃라인
            clonedMaterial.SetFloat("_OutlineWidth", 0.22f); // 글자를 확실히 돋보이게 해주는 적당한 두께
        }

        // 0.35초간 데미지가 머무른 뒤 페이드 아웃 및 축소 애니메이션을 시작하도록 설정
        fadeTimer = 0.35f;
    }

    private void Update()
    {
        // 1. 크기 애니메이션: 머무르는 시간(fadeTimer)이 끝나면 0으로 부드럽게 축소(shrink)됩니다.
        Vector3 currentTargetScale = fadeTimer <= 0f ? Vector3.zero : targetScale;
        transform.localScale = Vector3.Lerp(transform.localScale, currentTargetScale, Time.deltaTime * scaleSpeed);

        // 2. 물리 감속 상승 모션 (자연스러운 공기 저항이 느껴지도록 감속 처리)
        transform.position += new Vector3(moveSpeedX, moveSpeedY, 0f) * Time.deltaTime;
        moveSpeedY = Mathf.Lerp(moveSpeedY, 0f, Time.deltaTime * 3f);
        moveSpeedX = Mathf.Lerp(moveSpeedX, 0f, Time.deltaTime * 3f);

        // 3. 머무른 뒤 페이드 아웃 및 자동 파괴
        fadeTimer -= Time.deltaTime;
        if (fadeTimer <= 0f)
        {
            alpha -= fadeSpeed * Time.deltaTime;
            UpdateTextColor();

            if (alpha <= 0f || transform.localScale.x <= 0.05f)
            {
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// 알파 값의 변화를 반영하여 정밀하게 그라데이션 컬러를 업데이트합니다.
    /// </summary>
    private void UpdateTextColor()
    {
        if (textMesh == null) return;

        byte aByte = (byte)Mathf.Clamp(alpha * 255f, 0f, 255f);
        Color32 currentTop = new Color32(topColor.r, topColor.g, topColor.b, aByte);
        Color32 currentBottom = new Color32(bottomColor.r, bottomColor.g, bottomColor.b, aByte);
        
        textMesh.colorGradient = new VertexGradient(currentTop, currentTop, currentBottom, currentBottom);
    }

    private void OnDestroy()
    {
        // 동적 복제된 머티리얼 인스턴스를 파괴하여 씬 누수(Memory Leak)를 철저히 예방합니다.
        if (clonedMaterial != null)
        {
            Destroy(clonedMaterial);
        }
    }
}
