using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// 实现普通的单例模式
// where 限制模板的类型 ， new() 指的是这个类型必须要能被实例化
public class Singleton<T> where T : new()
{
    private static T _instance;
    private static readonly object mutex = new object();
    public static T instance
    {
        get
        {
            if (_instance == null)
            {
                lock (mutex)//保证我们的单例，是线程安全的
                {
                    if (_instance == null)
                    {
                        _instance = new T();
                    }
                }
            }
            return _instance;
        }
    }
}
//Monobeavior ： 声音 ， 网络
//Unity 单例
public class UnitySingleton<T> : MonoBehaviour
    where T : Component
{
    private static T _instance = null;
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                //是否有匹配的活动对象
                _instance = FindObjectOfType(typeof(T)) as T;
                if (_instance == null)
                {
                    GameObject obj = new GameObject();
                    _instance = (T)obj.AddComponent(typeof(T));
                    //对象不会保存到场景中。当一个新的场景被加载时，它不会被销毁
                    obj.hideFlags = HideFlags.DontSave;
                    obj.name = typeof(T).Name;
                }
            }
            return _instance;
        }
    }
    public virtual void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
        if (_instance == null)
        {
            _instance = this as T;
        }
        else
        {
            GameObject.Destroy(this.gameObject);
        }
    }
}