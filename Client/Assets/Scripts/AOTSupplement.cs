using System.Collections.Generic;
using LF.Network;
using UnityEngine;

/// <summary>
/// AOT 泛型方法桩补充清单。
///
/// === 背景 ===
/// HybridCLR + IL2CPP 构建下，热更程序集（GameLogic.dll）调用 mscorlib
/// 中的泛型方法时，需要 AOT 侧预先生成对应的方法桩。HybridCLR 的
/// "Generate > AOTGenericReference" 菜单会自动生成 AOTGenericReferences.cs，
/// 但该工具对嵌套类型的非虚方法（如 Dictionary&lt;,&gt;.ValueCollection.GetEnumerator()）
/// 存在盲区，无法完全覆盖。
///
/// === 本文件作用 ===
/// 独立于 AOTGenericReferences.cs（后者会被 Generate 工具完整覆盖），
/// 作为 AOT 桩补充清单。利用 [RuntimeInitializeOnLoadMethod] 确保代码
/// 不被 IL2CPP 裁剪，从而在构建时强制生成所需泛型方法桩。
///
/// === 如何扩展 ===
/// 当需要在热更代码中对 AOT 值类型（struct / enum）使用以下模式时，
/// 在本文件的 ForceAOTStubs() 方法中按模板新增一段代码：
///
///   模板：
///   {
///       var dict = new Dictionary<int, NewStruct>();
///       dict.Add(0, default(NewStruct));
///       var e = dict.GetEnumerator(); e.MoveNext(); var _ = e.Current;
///       var ve = dict.Values.GetEnumerator(); ve.MoveNext(); var _v = ve.Current;
///   }
///
/// === 什么情况不需要加 ===
/// - 热更程序集中的类型（如 Player、PropType）→ HybridCLR 运行时处理
/// - 引用类型（如 GameObject、Transform、string）→ IL2CPP 共享泛型代码
/// - 仅用索引器/ContainsKey 等方法的字典 → 这些方法已在 AOT 列表中
/// </summary>
public static class AOTSupplement
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ForceAOTStubs()
    {
        // ====================================================================
        // Dictionary<TKey, TValue> — 枚举器桩
        // 覆盖 GetEnumerator() / Values.GetEnumerator() / Keys.GetEnumerator()
        // ====================================================================

        // -------- 项目特定 AOT 值类型 --------

        // Dictionary<int, RoomInfo> (RoomInfo 是 Network.dll 中的 struct，AOT 补丁程序集)
        ForceDictEnum<int, RoomInfo>();

        // -------- 通用值类型组合（覆盖常见 Key/Value 组合）--------

        // Dictionary<int, ...> — int 是最常用的字典键
        ForceDictEnum<int, int>();
        ForceDictEnum<int, float>();
        ForceDictEnum<int, bool>();

        // Dictionary<string, ...> — 虽然 string 是引用类型，但值类型 Value 仍需独立桩
        ForceDictEnum<string, int>();
        ForceDictEnum<string, float>();
        ForceDictEnum<string, bool>();

        // ====================================================================
        // List<T> — 枚举器桩（当 T 为 AOT 值类型时）
        // ====================================================================
        ForceListEnum<int>();
        ForceListEnum<float>();
        ForceListEnum<bool>();
    }

    // ====================================================================
    // 辅助方法（无需修改）
    // ====================================================================

    /// <summary>
    /// 为 Dictionary&lt;TKey, TValue&gt; 强制生成全部枚举器桩。
    /// </summary>
    private static void ForceDictEnum<TKey, TValue>()
    {
        var dict = new Dictionary<TKey, TValue>();
        // 必须真正 Add 并 MoveNext，防止 IL2CPP 将整个代码路径判定为不可达而裁剪
        dict.Add(default(TKey), default(TValue));
        var e = dict.GetEnumerator();
        e.MoveNext();
        var _kvp = e.Current;
        var ve = dict.Values.GetEnumerator();
        ve.MoveNext();
        var _v = ve.Current;
        var ke = dict.Keys.GetEnumerator();
        ke.MoveNext();
        var _k = ke.Current;
        // 虚构的分支让编译器认为这些变量"可能被使用"
        if (_k.Equals(default(TKey))) Debug.Log(_kvp);
    }

    /// <summary>
    /// 为 List&lt;T&gt; 强制生成枚举器桩。
    /// </summary>
    private static void ForceListEnum<T>()
    {
        var list = new List<T>();
        list.Add(default(T));
        var e = list.GetEnumerator();
        e.MoveNext();
        var _item = e.Current;
        if (list.Count > 99999) Debug.Log(_item);
    }
}
