using System;
using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public interface ICamerEffect
{
    void Initialize(Transform camera);
    void Enter();
    void Update();
    void Exit();
    void Dispose();
}

public class NoneCameraEffect : ICamerEffect
{
    private Player player;
    private Transform cameraTransform;
    private Vector3 offsetCamera = new Vector3(0, 0, -10);
    public void Initialize(Transform camera) 
    {
        cameraTransform = camera;
        EventDispatcher.AddObserver(this, MessageEvent.OnRegistSelfPlayer, OnPlayerSpawned, null);
    }
    public void Dispose()
    {
        cameraTransform = null;
        EventDispatcher.RemoveObserver(this, MessageEvent.OnRegistSelfPlayer, null);
    }
    public void Enter() 
    {
        
    }
    private bool CheckInvalid()
    {
        if (cameraTransform == null)
        {
            Debug.LogError("CameraTransform is null");
            return true;
        }
        if (GameManager.Instance.GameIsOver || player == null || player.isDead || player.PlayObj == null)
        {
            return true;
        }
        return false;
    }
    public void Update() 
    {
        if (CheckInvalid())
        {
            return;
        }
        Vector3 pos = player.PlayObj.position + offsetCamera;
        pos.x = Mathf.Clamp(pos.x, 0, Screen.width / 2);
        if (GameManager.Instance.gameModel != GameManager.GameModel.Infinity)
        {
            pos.y = Mathf.Clamp(pos.y, 0, 20);
        }
        pos.z = -10;
        cameraTransform.position = pos;
    }
    public void Exit() 
    {
        
    }
    private bool OnPlayerSpawned(params object[] args)
    {
        player = args != null ? args[0] as Player : null;
        return false;
    }
}

public class CameraShakeEffect : ICamerEffect
{
    private Transform cameraTransform;
    private Vector3 originalPosition;
    private float shakeDuration = 0.3f;
    private float shakeMagnitude = 0.2f;
    private float dampingSpeed = 1.0f;
    public CameraShakeEffect() { }
    public CameraShakeEffect(float duration, float magnitude, float dampingSpeed)
    {
        this.shakeDuration = duration;
        this.shakeMagnitude = magnitude;
        this.dampingSpeed = dampingSpeed;
    }
    public void Initialize(Transform carmera)
    {
        cameraTransform = carmera;
    }
    public void Dispose()
    {
        cameraTransform = null;
    }
    public void Enter()
    {
        originalPosition = cameraTransform.localPosition;
    }
    public void Update()
    {
        if (shakeDuration > 0)
        {
            cameraTransform.localPosition = originalPosition + UnityEngine.Random.insideUnitSphere * shakeMagnitude;
            shakeDuration -= Time.deltaTime * dampingSpeed;
        }
        else
        {
            CameraController.instance.TranslateCameraEffect(CameraEffect.None);
        }
    }
    public void Exit()
    {
        shakeDuration = 0f;
        cameraTransform.localPosition = originalPosition;
    }
}
}
