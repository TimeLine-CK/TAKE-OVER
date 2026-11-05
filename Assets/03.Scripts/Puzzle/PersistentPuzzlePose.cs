/// <summary>
/// 스크립트 최종 수정일: 2026-11-02
/// 기능: 씬 재진입 시 카트 위치와 거울의 조절 각도를 복원한다.
/// </summary>
using UnityEngine;
public sealed class PersistentPuzzlePose : MonoBehaviour
{
    public string StateId;
    private bool started;
    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(StateId))
            StateId = gameObject.scene.path + "/" + name;
    }
    private void Start()
    {
        started = true;
        if (!StageProgress.Poses.TryGetValue(StateId, out Pose pose))
            return;
        transform.SetPositionAndRotation(pose.position, pose.rotation);
        if (TryGetComponent(out Rigidbody2D body))
        {
            body.position = pose.position;
            body.rotation = pose.rotation.eulerAngles.z;
        }
        Physics2D.SyncTransforms();
    }
    private void LateUpdate()
    {
        Save();
    }
    private void OnDisable()
    {
        Save();
    }
    private void Save()
    {
        if (started)
            StageProgress.Poses[StateId] = new Pose(transform.position, transform.rotation);
    }
}
