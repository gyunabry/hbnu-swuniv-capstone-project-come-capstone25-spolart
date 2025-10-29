using UnityEngine;

public class NPCInteractable : MonoBehaviour
{
    [Header("알림 오브젝트")]
    [SerializeField] private GameObject indicator;

    private Vector3 baseLoaclPosition;

    private void Awake()
    {
        if (indicator != null)
        {
            indicator.SetActive(false); // 꺼진 상태로 시작
        }
    }

    public void ShowInteractIndicator(bool show)
    {
        if (indicator == null) return;
        indicator.SetActive(show);
    }
}
