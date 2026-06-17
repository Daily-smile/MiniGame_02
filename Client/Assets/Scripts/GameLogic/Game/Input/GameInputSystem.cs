using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public enum InputModel
{
    Default,
    Phone,
}
public class GameInputSystem : Singleton<GameInputSystem>
{
#if UNITY_ANDROID || UNITY_IOS
    private InputModel inputModel = InputModel.Phone;
#else
    private InputModel inputModel = InputModel.Default;
#endif

    public InputModel InputModel => inputModel;

    public Vector2 Move
    {
        get
        {
            return GetMoveInput();
        }
    }
    public bool JumpDown
    {
        get
        {
            return GetJumpDownInput();
        }
    }
    public bool JumpHeld
    {
        get
        {
            return GetJumpHeldInput();
        }
    }

    public bool Fire
    {
        get
        {
            return GetFireInput();
        }
    }

    private bool GetJumpDownInput()
    {
        if (!GameManager.Instance.IsGameRuningState)
        {
            return false;
        }
        if (inputModel == InputModel.Default)
        {
            return Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.C);
        }
        else
        {
            return VirtualInputSystem.Instance.GetButtonDown("Jump");
        }
    }
    private bool GetJumpHeldInput()
    {
        if (!GameManager.Instance.IsGameRuningState)
        {
            return false;
        }
        if (inputModel == InputModel.Default)
        {
            return Input.GetButton("Jump") || Input.GetKey(KeyCode.C);
        }
        else
        {
            return VirtualInputSystem.Instance.GetButton("Jump");
        }
    }
    private Vector2 GetMoveInput()
    {
        if (!GameManager.Instance.IsGameRuningState)
        {
            return Vector2.zero;
        }
        if (inputModel == InputModel.Default)
        {
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }
        else
        {
            return new Vector2(VirtualInputSystem.Instance.GetAxisRaw("Horizontal"), VirtualInputSystem.Instance.GetAxisRaw("Vertical"));
        }
    }

    private bool GetFireInput()
    {
        if (!GameManager.Instance.IsGameRuningState)
        {
            return false;
        }
        if (inputModel == InputModel.Default)
        {
            return Input.GetKeyDown(KeyCode.F);
        }
        else
        {
            return VirtualInputSystem.Instance.GetButtonDown("Fire");
        }
    }
}
}
