using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家基础数值显示：经验条、等级、金币、能量。
/// 挂在 BaseProperty 上，自动查找子级 UI，并订阅 GameManager 的数值变化事件。
/// </summary>
public class PlayerStatusUI : MonoBehaviour
{
    [Header("UI 引用（留空自动查找）")]
    public Slider expSlider;
    public Text expText;
    public Text levelText;
    public Text coinText;
    public Text energyText;

    private GameManager gameManager;

    private void Awake()
    {
        ResolveRefs();
    }

    private void OnEnable()
    {
        gameManager = GameManager.Instance;
        if (gameManager != null)
            gameManager.OnStatsChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (gameManager != null)
            gameManager.OnStatsChanged -= Refresh;

        gameManager = null;
    }

    private void ResolveRefs()
    {
        if (expSlider == null)
            expSlider = FindComponent<Slider>("Exp/ExpSlider");

        if (expText == null)
            expText = FindComponent<Text>("Exp/Text (Legacy)");

        if (levelText == null)
            levelText = FindComponent<Text>("Exp/Image/Text (Legacy)");

        if (coinText == null)
            coinText = FindComponent<Text>("Coin/Background/Text (Legacy)");

        if (energyText == null)
            energyText = FindComponent<Text>("Energy/Background/Text (Legacy)");
    }

    private T FindComponent<T>(string path) where T : Component
    {
        Transform t = transform.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }

    /// <summary>把 GameManager 的当前数值刷到 UI 上</summary>
    public void Refresh()
    {
        if (gameManager == null)
            gameManager = GameManager.Instance;

        if (gameManager == null)
            return;

        int need = gameManager.ExpForLevel;

        if (expSlider != null)
            expSlider.value = need > 0 ? Mathf.Clamp01((float)gameManager.exp / need) : 0f;

        if (expText != null)
            expText.text = $"{gameManager.exp}/{need}";

        if (levelText != null)
            levelText.text = gameManager.level.ToString();

        if (coinText != null)
            coinText.text = gameManager.coins.ToString();

        if (energyText != null)
            energyText.text = gameManager.energy.ToString();
    }
}
