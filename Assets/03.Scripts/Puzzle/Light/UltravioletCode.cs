/// <summary>
/// 스크립트 최종 수정일: 2026-11-03
/// 기능: 코드 종류를 유지하고 자외선 노출 또는 상시 표시 설정에 따라 아이콘을 표시한다.
/// </summary>

using System.Collections.Generic;
using UnityEngine;

public sealed class UltravioletCode : MonoBehaviour
{
    public SpriteRenderer Icon;
    public Sprite[] CodeSprites = new Sprite[5];
    public bool RandomizeOnStart = true;
    public bool AlwaysVisible;
    public string CodeId;
    [Range(0, 4)] public int CodeIndex;

    private readonly HashSet<UltravioletLight> lights = new();
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// 아직 비활성화된 차단기도 같은 저장 코드로 연결할 수 있도록 초기화한다.
    /// </summary>
    public void Initialize()
    {
        if (initialized)
            return;
        initialized = true;
        if (string.IsNullOrWhiteSpace(CodeId))
        {
            string path = name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            CodeId = gameObject.scene.path + "/" + path;
        }
        if (StageProgress.Codes.TryGetValue(CodeId, out int saved))
            CodeIndex = saved;
        else if (RandomizeOnStart)
            CodeIndex = Random.Range(0, 5);
        SetCode(CodeIndex);
    }

    public void SetCode(int index)
    {
        CodeIndex = Mathf.Clamp(index, 0, 4);
        if (!string.IsNullOrWhiteSpace(CodeId))
            StageProgress.Codes[CodeId] = CodeIndex;
        if (Icon != null)
        {
            if (CodeSprites != null && CodeIndex < CodeSprites.Length && CodeSprites[CodeIndex] != null)
                Icon.sprite = CodeSprites[CodeIndex];
            Icon.enabled = AlwaysVisible || lights.Count > 0;
        }
    }

    public void SetIlluminated(UltravioletLight source, bool illuminated)
    {
        if (illuminated)
            lights.Add(source);
        else
            lights.Remove(source);
        if (Icon != null)
            Icon.enabled = isActiveAndEnabled && (AlwaysVisible || lights.Count > 0);
    }

    private void OnDisable()
    {
        lights.Clear();
        if (Icon != null)
            Icon.enabled = false;
    }

    private void OnEnable()
    {
        if (Icon != null)
            Icon.enabled = AlwaysVisible || lights.Count > 0;
    }
}
