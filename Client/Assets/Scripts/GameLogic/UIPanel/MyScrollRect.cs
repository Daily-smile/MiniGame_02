using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LF.Framework;
using LF.Network;

namespace LF.GameLogic
{
public class MyScrollRect : ScrollRect
{
    public bool isDrag {  get; private set; }
    public bool isDraging {  get; private set; }
    public override void OnBeginDrag(PointerEventData eventData)
    {
        isDrag = true;
        isDraging = false;
        base.OnBeginDrag(eventData);
    }

    public override void OnDrag(PointerEventData eventData)
    {
        isDraging = true;
        base.OnDrag(eventData);
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        isDrag = false;
        isDraging = false;
        base.OnEndDrag(eventData);
    }
}
}
