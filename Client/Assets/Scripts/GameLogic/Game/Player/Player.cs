using System;
using System.Collections.Generic;
using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public enum PlayerWeaponType
{
    None = 0,
    Fireball,
}

public enum PropType
{
    None = 0,
    bomb,
}

public class Player : IDisposable
{
    private int _hp;
    public int HP
    {
        get { return _hp; }
        private set
        {
            if (_hp != value)
            {
                _hp = value;
                onHPChanged?.Invoke(_hp);
            }
        }
    }
    public string PlayName { get; private set; }
    private int _starCount; // 星星数量
    public int StarCount
    {
        get { return _starCount; }
        private set
        {
            if (_starCount != value)
            {
                _starCount = value;
                onStarChanged?.Invoke(_starCount);
            }
        }
    }
    public Transform PlayObj;
    public ContestantPlayer PlayerUI;
    public bool isSelf { get; private set; }
    public bool isWin { get; private set; }
    private bool _isDead;
    public bool isDead 
    {
        get { return _isDead; }
        private set
        {
            if (_isDead != value)
            {
                _isDead = value;
                onDeadChanged?.Invoke(_isDead);
            }
        }
    }
    private bool disposedValue;

    /// <summary>
    /// 无敌状态
    /// </summary>
    public bool invincibleState { get; private set; }
    public PlayerWeaponType weaponType { get; private set; } = PlayerWeaponType.None;
    public Dictionary<PropType, int> propList { get; private set; } = new Dictionary<PropType, int>();

    public event Action<bool> onDeadChanged;
    public event Action<int> onHPChanged;
    public event Action<int> onStarChanged;
    public event Action<PlayerWeaponType> onGetWeapon;
    public event Action<PropType, int> onPropChanged;

    public Player(string name)
    {
        this.HP = 3;
        this.PlayName = name;
        this.isSelf = name.Equals(GameManager.Instance.userName);
        this.PlayObj = null;
        this.StarCount = 0;
        this.invincibleState = false;
        AddObserverEvents();
    }

    private void AddObserverEvents()
    {
        EventDispatcher.AddObserver(this, MessageEvent.InGameGetStar, OnGetStar, null);
        EventDispatcher.AddObserver(this, MessageEvent.OnNetPlayerDead, OnNetPlayerDead, null);
        EventDispatcher.AddObserver(this, MessageEvent.PlayerOnHit, OnHit, null);
        EventDispatcher.AddObserver(this, MessageEvent.PlayerGetWeapon, OnGetWeapon, null);
        EventDispatcher.AddObserver(this, MessageEvent.PlayerGetProp, OnGetProp, null);
        EventDispatcher.AddObserver(this, MessageEvent.PlayerUseProp, OnUseProp, null);
        EventDispatcher.AddObserver(this, MessageEvent.ForceDead, ForceDead, null);
    }

    private void RemoveObserverEvents()
    {
        EventDispatcher.RemoveObserver(this, MessageEvent.InGameGetStar, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.OnNetPlayerDead, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.PlayerOnHit, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.PlayerGetWeapon, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.PlayerGetProp, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.PlayerUseProp, null);
        EventDispatcher.RemoveObserver(this, MessageEvent.ForceDead, null);
    }

    #region Dispose
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                PlayObj = null;
                PlayerUI = null;
                onHPChanged = null;
                onStarChanged = null;
            }

            EventDispatcher.PostEvent(MessageEvent.OnRegistSelfPlayer, this, null);
            RemoveObserverEvents();
            disposedValue = true;
        }
    }

    ~Player()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion

    public void OnQuitGame()
    {
        invincibleState = false;
        isWin = false;
        isDead = false;
        weaponType = PlayerWeaponType.None;
        onGetWeapon?.Invoke(weaponType);
        propList.Clear();
        onPropChanged?.Invoke(PropType.None, 0);
    }

    public void OnGameAgainOnSingleModel()
    {
        invincibleState = false;
        this.HP = 3;
        StarCount = 0;
        isWin = false;
        isDead = false;
        weaponType = PlayerWeaponType.None;
        onGetWeapon?.Invoke(weaponType);
        propList.Clear();
        onPropChanged?.Invoke(PropType.None, 0);
        if (PlayObj != null)
        {
            PlayerAnimator animtor = PlayObj.GetComponentInChildren<PlayerAnimator>();
            if (animtor != null)
            {
                animtor.OnResurrect();
            }
        }
    }
    public void OnGameAgainOnMultipleModel()
    {
        invincibleState = false;
        this.HP = 3;
        StarCount = 0;
        isWin = false;
        isDead = false;
        weaponType = PlayerWeaponType.None;
        onGetWeapon?.Invoke(weaponType);
        propList.Clear();
        onPropChanged?.Invoke(PropType.None, 0);
    }

    public void OnGameOver()
    {
        isWin = false;
        isDead = true;
    }

    public void OnGameWin()
    {
        isWin = true;
        isDead = false;
    }

    public void OnUpdateUI()
    {
        if (PlayerUI == null || PlayObj == null) return;
        if (GameReferee.instance == null || GameReferee.instance.destinationPoint == null) return;
        if (GameReferee.instance.destinationTotalDistance <= 0) return;
        PlayerUI.OnRefreshUI(Vector2.Distance(PlayObj.position, GameReferee.instance.destinationPoint.position) / (float)GameReferee.instance.destinationTotalDistance, StarCount, isDead);
    }

    /// <summary>
    /// 无敌状态结束
    /// </summary>
    private void InvincibleEnd()
    {
        invincibleState = false;
    }

    private bool OnGetStar(params object[] args)
    {
        string name = (string)args[0];
        if (name.Equals(PlayName))
        {
            StarCount++;
        }
        return false;
    }

    private bool OnNetPlayerDead(params object[] args)
    {
        string toName = (string)args[0];
        if (toName.Equals(PlayName))
        {
            isWin = false;
            isDead = true;
            // 联机模式下双方均应看到失败弹窗
            EventDispatcher.PostEvent(MessageEvent.GameOver, this, null);
        }
        return false;
    }

    private bool OnHit(params object[] args)
    {
        string toName = (string)args[0];
        if (!toName.Equals(PlayName))
        {
            return false;
        }
        bool isFullHit = args.Length < 2 ? false : (bool)args[1];
        if (invincibleState && !isFullHit) return false;
        invincibleState = !isFullHit;
        if ((this.HP - 1) <= 0)
        {
            this.HP = 0;
            isDead = true;
            EventDispatcher.PostEvent(MessageEvent.OnPlayerDead, this, null);
        }
        else
        {
            this.HP--;
            if (isSelf)
            {
                isDead = false;
                EventDispatcher.PostEvent(MessageEvent.OnPlayerFullRebirth, this, null);
            }
        }
        if (PlayObj != null)
        {
            PlayerAnimator animtor = PlayObj.GetComponentInChildren<PlayerAnimator>();
            if (animtor != null)
            {
                animtor.OnHit(isDead, InvincibleEnd);
            }

            // Mirror 网络同步：CmdTakeDamage→RpcOnHit 已包含伤害+视觉的完整同步
            MirrorPlayer mp = PlayObj.GetComponent<MirrorPlayer>();
            if (mp != null && isSelf)
            {
                mp.CmdTakeDamage(GameManager.Instance.userName, 1);
            }
        }
        return false;
    }

    private bool OnGetWeapon(params object[] args)
    {
        string name = (string)args[0];
        if (!name.Equals(PlayName)) return false;
        this.weaponType = (PlayerWeaponType)args[1];
        onGetWeapon?.Invoke(weaponType);
        return false;
    }

    private bool OnGetProp(params object[] args)
    {
        string name = (string)args[0];
        if (!name.Equals(PlayName)) return false;
        PropType propType = (PropType)args[1];
        if (propList.ContainsKey(propType))
        {
            propList[propType]++;
        }
        else
        {
            propList[propType] = 1;
        }
        onPropChanged?.Invoke(propType, propList[propType]);
        return false;
    }

    private bool OnUseProp(params object[] args)
    {
        string name = (string)args[0];
        if (!name.Equals(PlayName)) return false;
        PropType propType = (PropType)args[1];
        propList[propType]--;
        onPropChanged?.Invoke(propType, propList[propType]);
        if (isSelf)
        {
            GameObject bombPrefab = ResourceManager.Instance.LoadAsset<GameObject>("Props_Bomb");
            GameObject bombObj = GameObject.Instantiate(bombPrefab);
            bombObj.name = bombPrefab.name;
            bombObj.transform.position = PlayObj.position;
        }
        return false;
    }

    private bool ForceDead(params object[] args)
    {
        if (isDead)
        {
            return false;
        }
        isDead = true;
        EventDispatcher.PostEvent(MessageEvent.OnPlayerDead, this, null);
        if (PlayObj != null)
        {
            PlayerAnimator animtor = PlayObj.GetComponentInChildren<PlayerAnimator>();
            if (animtor != null)
            {
                animtor.OnHit(isDead, InvincibleEnd);
            }
        }
        return false;
    }
}
}
