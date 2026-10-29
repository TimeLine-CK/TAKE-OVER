/// <summary>
/// 스크립트 최종 수정일: 2026-10-30
/// 기능: 거울의 고정 여부를 구분하고 조작 가능한 거울의 각도를 변경한다.
/// </summary>

using UnityEngine;

public sealed class MirrorReflector2D : Interactable
{
    [SerializeField] private string mirrorId = "Mirror01"; // 과거와 현재 거울을 연결할 이름
    [SerializeField, Tooltip("거울 이미지와 반사면 콜라이더를 함께 자식으로 둔 회전축")]
    private Transform rotationPivot;
    [SerializeField, Min(0f)] private float rotationSpeed = 35f; // 초당 회전 각도
    [SerializeField, InspectorName("고정형 거울"), Tooltip("체크하면 배치 각도를 유지합니다. 해제하면 시작 시 무작위 각도가 적용되고 F를 누르는 동안 회전합니다.")]
    private bool isFixed;

    public string MirrorId => mirrorId;
    public bool IsFixed => isFixed;
    public float Angle
    {
        get;
        private set;
    }

    private MirrorPuzzleController controller;
    private float aimError;
    private int aimFrame = -10;

    public override bool CanInteract(TimeFlag currentFlag)
    {
        return !isFixed && controller != null && controller.isActiveAndEnabled && controller.IsReady
            && rotationPivot != null && base.CanInteract(currentFlag);
    }

    public override void HoldInteract(PlayerInteractor player, float deltaTime)
    {
        if (!isFixed && controller != null && rotationPivot != null)
        {
            float deltaAngle = Mathf.Max(0f, rotationSpeed) * Mathf.Max(0f, deltaTime);
            if (aimFrame >= Time.frameCount - 1)
            {
                // 계속 누르면 보정 지점을 지나갈 수 있도록 회전량의 20%는 유지한다.
                deltaAngle += Mathf.Clamp(aimError * 8f * Mathf.Max(0f, deltaTime),
                    -deltaAngle * 0.8f, deltaAngle);
            }
            controller.RotateMirror(this, deltaAngle);
        }
    }

    internal void SetAimError(float error)
    {
        if (rotationPivot == null)
            return;
        Transform parent = rotationPivot.parent;
        if (parent != null)
        {
            Vector3 right = parent.TransformVector(Vector3.right);
            Vector3 up = parent.TransformVector(Vector3.up);
            if (Vector3.Cross(right, up).z < 0f)
            {
                error = -error;
            }
        }
        if (aimFrame == Time.frameCount && Mathf.Abs(aimError) <= Mathf.Abs(error))
            return;

        aimError = error;
        aimFrame = Time.frameCount;
    }

    internal void Bind(MirrorPuzzleController source, float angle)
    {
        controller = source;
        if (isFixed)
        {
            if (rotationPivot != null)
                Angle = rotationPivot.localEulerAngles.z;
            return;
        }
        ApplyAngle(angle);
    }

    internal void ApplyAngle(float angle)
    {
        Angle = Mathf.Repeat(angle, 360f);
        if (rotationPivot != null)
            rotationPivot.localRotation = Quaternion.Euler(0f, 0f, Angle);
    }
}
