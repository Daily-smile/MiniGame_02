using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 摄像机效果
/// </summary>
public enum CameraEffect
{
    None,
    Shake, // 屏幕震动效果
}

public class CameraController : MonoBehaviour
{
    public static CameraController instance;
    private Dictionary<CameraEffect, ICamerEffect> effects;
    private CameraEffect currentCameraEffect = CameraEffect.None;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
        effects = new Dictionary<CameraEffect, ICamerEffect>();
        effects.Add(CameraEffect.None, new NoneCameraEffect());
        effects.Add(CameraEffect.Shake, new CameraShakeEffect());
        effects[CameraEffect.None].Initialize(transform);
        effects[CameraEffect.Shake].Initialize(transform);
    }

    private void OnDestroy()
    {
        foreach (var effect in effects.Values)
        {
            effect.Dispose();
        }
        effects.Clear();
        effects = null;
    }

    void LateUpdate()
    {
        effects[currentCameraEffect].Update();
    }

    public void TranslateCameraEffect(CameraEffect effect)
    {
        if (currentCameraEffect != effect)
        {
            effects[currentCameraEffect].Exit();
            currentCameraEffect = effect;
            effects[currentCameraEffect].Enter();
        }
    }
}