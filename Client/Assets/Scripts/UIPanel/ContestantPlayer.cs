using System.Text;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class ContestantPlayer : BaseUI
{
    [HideInInspector]
    public string playName;
    [HideInInspector]
    public float progress;
    [HideInInspector]
    public int starCount;
    private bool isDead;
    /// <summary>
    /// 是否玩家自己
    /// </summary>
    private bool isSelf;
    private GameUIPanel rootPanel;
    private StringBuilder strBuild1;
    private StringBuilder strBuild2;

    private TextMeshProUGUI playerNameText, progressText;
    private Transform[] starsTran;
    private Transform deadTran;
    private Slider progressSlider;

    public void Initialize(string name, bool isPlayerSelf, GameUIPanel ui)
    {
        this.playName = name;
        this.isSelf = isPlayerSelf;
        this.starCount = 0;
        this.progress = 0;
        this.rootPanel = ui;
        this.isDead = false;
        InitTran();
    }
    private void InitTran()
    {
        playerNameText = transform.Find("name").GetComponent<TextMeshProUGUI>();
        if (isSelf)
        {
            playerNameText.text = "【本人】";
            playerNameText.color = new Color32(255, 218, 0, 255);
        }
        else
        {
            playerNameText.text = playName;
            playerNameText.color = Color.white;
        }
        progressText = transform.Find("progressValue").GetComponent<TextMeshProUGUI>();
        strBuild1 = new StringBuilder("0");
        strBuild2 = new StringBuilder("%");
        progressText.text = strBuild1.Append(strBuild2).ToString();
        deadTran = transform.Find("dead");
        deadTran.gameObject.SetActive(false);
        Transform starRoot = transform.Find("star");
        starsTran = new Transform[starRoot.childCount];
        for (int i = 0; i < starRoot.childCount; i++)
        {
            starsTran[i] = starRoot.GetChild(i);
            starsTran[i].gameObject.SetActive(false);
        }
        progressSlider = transform.Find("progress").GetComponent<Slider>();
        progressSlider.value = progress;
    }

    public void OnRefreshUI(float progressV, int starNum, bool dead)
    {
        progressV = Mathf.Clamp01(1 - progressV);
        progress = progressV;
        starCount = starNum;
        isDead = dead;
        strBuild1.Clear();
        strBuild1.Append((progress * 100).ToString("0.0")).Append(strBuild2);
        progressText.text = strBuild1.ToString();
        progressSlider.value = progress;
        for (int i = 0; i < starsTran.Length; i++)
        {
            starsTran[i].gameObject.SetActive(starCount > i);
        }
        deadTran.gameObject.SetActive(isDead);
    }
}