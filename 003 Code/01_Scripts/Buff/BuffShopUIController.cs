using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public class BuffShopUIController : MonoBehaviour
{
    [Header("목록/프리팹")]
    [SerializeField] private Transform listContainer;
    [SerializeField] private BuffListItem listItemPrefab;

    [Header("상세정보")]
    [SerializeField] private BuffDetailPanel detailPanel;
    

    private readonly List<BuffListItem> _items = new();
    private BuffListItem _selected;

    private void Start()
    {
        BuildList();
        if (detailPanel) detailPanel.Clear();
    }

    private void BuildList()
    {
        // 기존 아이템 클리어
        for (int i = listContainer.childCount - 1; i >= 0; i--)
            Destroy(listContainer.GetChild(i).gameObject);
        _items.Clear();

        // 런버프 매니저에서 데이터 로드
        var mgr = DungeonRunBuffManager.Instance;
        List<BuffData> source = null;
        if (mgr != null && mgr.Catalog != null && mgr.Catalog.Count > 0)
        {
            source = new List<BuffData>(mgr.Catalog);
        }

        foreach (var data in source)
        {
            var item = Instantiate(listItemPrefab, listContainer);
            item.Setup(data, OnClickItem);
            _items.Add(item);
        }

        if (_items.Count > 0)
        {
            OnClickItem(_items[0]);
        }
    }

    private void OnClickItem(BuffListItem item)
    {
        if (item == null) return;

        if (_selected != null) _selected.SetSelected(false);
        _selected = item;
        _selected.SetSelected(true);

        // 상세 패널 표시
        if (detailPanel) detailPanel.Show(item.Data);
    }
}
