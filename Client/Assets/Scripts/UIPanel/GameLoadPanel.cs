using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameLoadPanel : BasePanel
{
    Animator animator;
    Slider progressSlider;
    TextMeshProUGUI loadText;
    Coroutine loadGrogress;
    float timer;
    float enterAnimTotalTime;
    float quitAnimTotalTime;
    string[] loadAssetPaths;
    public override void Init()
    {
        panelType = UIPanelType.GameLoad;
        animator = transform.Find("LoadAnim").GetComponent<Animator>();
        AnimationClip[] animInfos = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < animInfos.Length; i++)
        {
            AnimationClip info = animInfos[i];
            if (info.name.Equals("GameEnterLoad"))
            {
                enterAnimTotalTime = info.length;
            }
            if (info.name.Equals("GameQuitLoad"))
            {
                quitAnimTotalTime = info.length;
            }
        }
        progressSlider = transform.Find("Slider").GetComponent<Slider>();
        loadText = transform.Find("load").GetComponent<TextMeshProUGUI>();
    }
    public override void Dispose()
    {
        
    }

    public override void OnEnter(Action<BasePanel> callback = null)
    {
        base.OnEnter(null);
        OnOpenUI();
        callback?.Invoke(this);
    }

    public override void OnRecovery(Action<BasePanel> callback = null)
    {
        base.OnRecovery(null);
        OnOpenUI();
        callback?.Invoke(this);
    }

    public override void OnPause(Action<BasePanel> callback = null)
    {
        base.OnPause(null);
        OnCloseUI();
        callback?.Invoke(this);
    }

    public override void OnExist(Action<BasePanel> callback = null)
    {
        base.OnExist(null);
        OnCloseUI();
        callback?.Invoke(this);
    }

    private void OnOpenUI()
    {
        progressSlider.value = 0;
        loadText.text = MessageEvent.allMessageStr[MessageEventType.LoadingInitializeStr];
    }

    private void OnCloseUI()
    {
        if (loadGrogress != null)
        {
            StopCoroutine(loadGrogress);
        }
    }

    public void OnEnterGameScene()
    {
        OnCloseUI();
        animator.Play("GameLoad", 0, 0);
        loadGrogress = StartCoroutine(LoadProgressOnEnterGame());
    }
    public void OnQuitGameScene()
    {
        OnCloseUI();
        animator.Play("GameQuitLoad", 0, 0);
        loadGrogress = StartCoroutine(LoadProgressOnQuitGame());
    }

    private IEnumerator LoadEnterInfinityGame(params string[] pathArr)
    {
        yield return null;
        loadAssetPaths = pathArr;
        float addNum = 0.75f / pathArr.Length;
        timer = 0;
        for (int i = 0; i < pathArr.Length; i++)
        {
            ResourceRequest progress = Resources.LoadAsync<GameObject>(pathArr[i]);
            while (true)
            {
                if (timer < enterAnimTotalTime * addNum)
                {
                    timer += Time.deltaTime;
                    float curProgressOffset = Mathf.Clamp01(1 - (float)progress.progress) * addNum;
                    float curProgress = Mathf.Clamp(timer / enterAnimTotalTime - curProgressOffset, 0, addNum);
                    float newProgress = 0.25f + curProgress + addNum * i;
                    progressSlider.value = newProgress * 100f;
                    animator.Play("GameLoad", 0, newProgress);
                }
                else if (progress.isDone)
                {
                    GameObject obj = progress.asset as GameObject;
                    ResourceManager.Instance.AddAsset(pathArr[i], obj);
                    float newProgress = 0.25f + addNum * (i + 1);
                    progressSlider.value = newProgress * 100f;
                    animator.Play("GameLoad", 0, newProgress);
                    break;
                }
                else
                {
                    timer = enterAnimTotalTime * addNum;
                }
                yield return new WaitForEndOfFrame();
            }
            timer = 0;
            yield return new WaitForEndOfFrame();
        }
        animator.enabled = false;
        GameReferee.instance.GeneratePlayer();
        yield break;
    }

    private IEnumerator LoadEnterGameAssets(params string[] pathArr)
    {
        loadAssetPaths = pathArr;
        float addNum = 0.75f / pathArr.Length;
        timer = 0;
        for (int i = 0; i < pathArr.Length; i++)
        {
            ResourceRequest progress = Resources.LoadAsync<GameObject>(pathArr[i]);
            while (true)
            {
                if (timer < enterAnimTotalTime * addNum)
                {
                    timer += Time.deltaTime;
                    float curProgressOffset = Mathf.Clamp01(1 - (float)progress.progress) * addNum;
                    float curProgress = Mathf.Clamp(timer / enterAnimTotalTime - curProgressOffset, 0, addNum);
                    float newProgress = 0.25f + curProgress + addNum * i;
                    progressSlider.value = newProgress * 100f;
                    animator.Play("GameLoad", 0, newProgress);
                }
                else if (progress.isDone)
                {
                    GameObject obj = progress.asset as GameObject;
                    ResourceManager.Instance.AddAsset(pathArr[i], obj);
                    float newProgress = 0.25f + addNum * (i + 1);
                    progressSlider.value = newProgress * 100f;
                    animator.Play("GameLoad", 0, newProgress);
                    break;
                }
                else
                {
                    timer = enterAnimTotalTime * addNum;
                }
                yield return new WaitForEndOfFrame();
            }
            timer = 0;
            yield return new WaitForEndOfFrame();
        }
        animator.enabled = false;
        GameReferee.instance.GenerateGameScene();
        yield return new WaitForSeconds(0.1f);
    }

    private IEnumerator LoadProgressOnEnterGame()
    {
        animator.enabled = true;
        AsyncOperation loadScene = SceneManager.LoadSceneAsync(1, LoadSceneMode.Single);
        loadText.text = MessageEvent.allMessageStr[MessageEventType.LoadingSceneStr];
        timer = 0;
        while (true)
        {
            if (timer < enterAnimTotalTime * 0.25f)
            {
                timer += Time.deltaTime;
            }
            else if (loadScene.isDone)
            {
                loadText.text = MessageEvent.allMessageStr[MessageEventType.LoadingAssetsStr];
                if (GameManager.Instance.gameModel != GameManager.GameModel.Infinity)
                {
                    string[] loadPaths = new string[] { "Maps/Environment", "Roles/Player", "Roles/NetPlayer", "Bullets/Fireball" };
                    yield return LoadEnterGameAssets(loadPaths);
                }
                else
                {
                    string[] loadPaths = new string[] { "Roles/Player" };
                    yield return LoadEnterInfinityGame(loadPaths);
                }
                break;
            }
            else
            {
                timer = enterAnimTotalTime * 0.25f;
            }
            float progress = Mathf.Clamp01(1 - loadScene.progress) * 0.25f;
            progress = Mathf.Clamp(timer / enterAnimTotalTime - progress, 0, 0.25f);
            progressSlider.value = progress * 100f;
            animator.Play("GameLoad", 0, progress);
            yield return new WaitForEndOfFrame();
        }
        loadGrogress = null;
        UIManager.instance.OpenPanel(UIPanelType.GameUI, (panel) => {
            GameUIPanel ui = panel as GameUIPanel;
            GameReferee.instance.OnGameUIPanelShow(ui, out Player selfPlay);
            ui.Initialize(selfPlay);
            if (GameManager.Instance.gameModel == GameManager.GameModel.Infinity)
            {
                GameObject generatorPrefab = Resources.Load<GameObject>("PlatformGenerator");
                GameObject generator = GameObject.Instantiate(generatorPrefab);
                InfinitePlatformGenerator platformGenerator = generator.GetComponent<InfinitePlatformGenerator>();
                platformGenerator.player = selfPlay.PlayObj;
            }
            else if (selfPlay.PlayObj != null)
            {
                GameObject.Destroy(selfPlay.PlayObj.GetComponent<PlayerDeathCheck>());
            }
            EventDispatcher.PostEvent(MessageEvent.OnRegistSelfPlayer, this, selfPlay);
            // 单人模式和无限模式不需要等待其他玩家，直接开始
            bool isOnlineMultiplayer = GameManager.Instance.IsLoginServer && GameManager.Instance.connectState
                && GameManager.Instance.gameModel != GameManager.GameModel.Single
                && GameManager.Instance.gameModel != GameManager.GameModel.Infinity;
            if (isOnlineMultiplayer)
                CustomNetworkManager.singleton.SendEnterGameScene(GameManager.Instance.GetCurServerGameModel(), GameManager.Instance.GetRoomID);
            ui.WaitingForGameStart(isOnlineMultiplayer);
        });
    }

    private IEnumerator LoadProgressOnQuitGame()
    {
        animator.enabled = true;
        ObjectPool.instance.ClearPool("Fireball");
        AsyncOperation load = SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
        timer = 0;
        float progress = 0;
        loadText.text = MessageEvent.allMessageStr[MessageEventType.LoadingSceneStr];
        while (true)
        {
            if (load.isDone)
            {
                loadText.text = MessageEvent.allMessageStr[MessageEventType.LoadingAssetsStr];
                if (timer >= quitAnimTotalTime)
                {
                    progressSlider.value = 100;
                    animator.Play("GameQuitLoad", 0, 1f);
                    animator.enabled = false;
                    yield return new WaitForSeconds(0.2f);
                    break;
                }
            }
            timer += Time.deltaTime;
            float toProgress = Mathf.Clamp01(1 - load.progress);
            progress = Mathf.Clamp01(timer / quitAnimTotalTime - toProgress);
            animator.Play("GameQuitLoad", 0, progress);
            progressSlider.value = progress * 100;
            yield return new WaitForEndOfFrame();
        }
        loadGrogress = null;
        if (loadAssetPaths != null)
        {
            for (int i = 0; i < loadAssetPaths.Length; i++)
            {
                ResourceManager.Instance.UnloadAsset(loadAssetPaths[i]);
            }
        }
        loadAssetPaths = null;
        if (GameManager.Instance.IsLoginServer)
            UIManager.instance.OpenPanel(UIPanelType.Game);
        else
            UIManager.instance.OpenPanel(UIPanelType.Start);
    }
}
