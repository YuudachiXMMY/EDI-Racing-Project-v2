using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple tab selection component. Manages highlight state and content visibility.
/// </summary>
public class TabButton : MonoBehaviour
{
    public Button Button;
    public GameObject TabContent;
    public Text Label;

    private static readonly Color SelectedColor = new Color(0.3f, 0.5f, 0.8f, 1f);
    private static readonly Color NormalColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);

    public void SetSelected(bool selected)
    {
        if (TabContent != null)
            TabContent.SetActive(selected);

        var img = Button != null ? Button.GetComponent<Image>() : null;
        if (img != null)
            img.color = selected ? SelectedColor : NormalColor;
    }

    public static void SelectTab(TabButton[] tabs, int index)
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i] != null)
                tabs[i].SetSelected(i == index);
        }
    }
}
