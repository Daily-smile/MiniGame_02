using System.Collections.Generic;
public class AOTGenericReferences : UnityEngine.MonoBehaviour
{

	// {{ AOT assemblies
	public static readonly IReadOnlyList<string> PatchedAOTAssemblyList = new List<string>
	{
		"DOTween.dll",
		"LFFramework.dll",
		"UnityEngine.CoreModule.dll",
		"YooAsset.dll",
		"mscorlib.dll",
	};
	// }}

	// {{ constraint implement type
	// }} 

	// {{ AOT generic types
	// LF.Framework.EventDispatcher.<>c__DisplayClass13_0<object>
	// LF.Framework.EventDispatcher.ObserverInfo<object>
	// LF.Framework.EventDispatcher<object>
	// LF.Framework.ResourceManager.<>c__DisplayClass25_0<object>
	// LF.Framework.Singleton<object>
	// LF.Framework.UnitySingleton<object>
	// System.Action<LF.Framework.EventDispatcher.ObserverInfo<object>>
	// System.Action<UnityEngine.Vector3>
	// System.Action<byte,float>
	// System.Action<byte>
	// System.Action<int,int>
	// System.Action<int>
	// System.Action<object>
	// System.Collections.Generic.ArraySortHelper<LF.Framework.EventDispatcher.ObserverInfo<object>>
	// System.Collections.Generic.ArraySortHelper<object>
	// System.Collections.Generic.Comparer<LF.Framework.EventDispatcher.ObserverInfo<object>>
	// System.Collections.Generic.Comparer<object>
	// System.Collections.Generic.ComparisonComparer<LF.Framework.EventDispatcher.ObserverInfo<object>>
	// System.Collections.Generic.ComparisonComparer<object>
	// System.Collections.Generic.Dictionary.Enumerator<int,LF.Network.RoomInfo>
	// System.Collections.Generic.Dictionary.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,LF.Network.RoomInfo>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection<int,LF.Network.RoomInfo>
	// System.Collections.Generic.Dictionary.KeyCollection<int,int>
	// System.Collections.Generic.Dictionary.KeyCollection<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,LF.Network.RoomInfo>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection<int,LF.Network.RoomInfo>
	// System.Collections.Generic.Dictionary.ValueCollection<int,int>
	// System.Collections.Generic.Dictionary.ValueCollection<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection<object,object>
	// System.Collections.Generic.Dictionary<int,LF.Network.RoomInfo>
	// System.Collections.Generic.Dictionary<int,int>
	// System.Collections.Generic.Dictionary<int,object>
	// System.Collections.Generic.Dictionary<object,object>
	// System.Collections.Generic.EqualityComparer<LF.Network.RoomInfo>
	// System.Collections.Generic.EqualityComparer<int>
	// System.Collections.Generic.EqualityComparer<object>
	// System.Collections.Generic.ICollection<LF.Framework.EventDispatcher.ObserverInfo<object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,LF.Network.RoomInfo>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.ICollection<object>
	// System.Collections.Generic.IComparer<LF.Framework.EventDispatcher.ObserverInfo<object>>
	// System.Collections.Generic.IComparer<object>
	// System.Collections.Generic.IEnumerable<LF.Framework.EventDispatcher.ObserverInfo<object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,LF.Network.RoomInfo>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerable<object>
	// System.Collections.Generic.IEnumerator<LF.Framework.EventDispatcher.ObserverInfo<object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,LF.Network.RoomInfo>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerator<object>
	// System.Collections.Generic.IEqualityComparer<int>
	// System.Collections.Generic.IEqualityComparer<object>
	// System.Collections.Generic.IList<LF.Framework.EventDispatcher.ObserverInfo<object>>
	// System.Collections.Generic.IList<object>
	// System.Collections.Generic.KeyValuePair<int,LF.Network.RoomInfo>
	// System.Collections.Generic.KeyValuePair<int,int>
	// System.Collections.Generic.KeyValuePair<int,object>
	// System.Collections.Generic.KeyValuePair<object,object>
	// System.Collections.Generic.List.Enumerator<LF.Framework.EventDispatcher.ObserverInfo<object>>
	// System.Collections.Generic.List.Enumerator<object>
	// System.Collections.Generic.List<LF.Framework.EventDispatcher.ObserverInfo<object>>
	// System.Collections.Generic.List<object>
	// System.Collections.Generic.ObjectComparer<LF.Framework.EventDispatcher.ObserverInfo<object>>
	// System.Collections.Generic.ObjectComparer<object>
	// System.Collections.Generic.ObjectEqualityComparer<LF.Network.RoomInfo>
	// System.Collections.Generic.ObjectEqualityComparer<int>
	// System.Collections.Generic.ObjectEqualityComparer<object>
	// System.Collections.Generic.Queue.Enumerator<object>
	// System.Collections.Generic.Queue<object>
	// System.Collections.ObjectModel.ReadOnlyCollection<LF.Framework.EventDispatcher.ObserverInfo<object>>
	// System.Collections.ObjectModel.ReadOnlyCollection<object>
	// System.Comparison<LF.Framework.EventDispatcher.ObserverInfo<object>>
	// System.Comparison<object>
	// System.Predicate<LF.Framework.EventDispatcher.ObserverInfo<object>>
	// System.Predicate<object>
	// UnityEngine.Events.InvokableCall<UnityEngine.Vector2>
	// UnityEngine.Events.InvokableCall<byte>
	// UnityEngine.Events.InvokableCall<float>
	// UnityEngine.Events.InvokableCall<object>
	// UnityEngine.Events.UnityAction<UnityEngine.Vector2>
	// UnityEngine.Events.UnityAction<byte>
	// UnityEngine.Events.UnityAction<float>
	// UnityEngine.Events.UnityAction<object>
	// UnityEngine.Events.UnityEvent<UnityEngine.Vector2>
	// UnityEngine.Events.UnityEvent<byte>
	// UnityEngine.Events.UnityEvent<float>
	// UnityEngine.Events.UnityEvent<object>
	// }}

	public void RefMethods()
	{
		// object DG.Tweening.TweenExtensions.Pause<object>(object)
		// object DG.Tweening.TweenExtensions.Play<object>(object)
		// object DG.Tweening.TweenSettingsExtensions.OnComplete<object>(object,DG.Tweening.TweenCallback)
		// object DG.Tweening.TweenSettingsExtensions.SetAutoKill<object>(object,bool)
		// object DG.Tweening.TweenSettingsExtensions.SetEase<object>(object,DG.Tweening.Ease)
		// object DG.Tweening.TweenSettingsExtensions.SetLoops<object>(object,int)
		// object DG.Tweening.TweenSettingsExtensions.SetLoops<object>(object,int,DG.Tweening.LoopType)
		// System.Void LF.Framework.EventDispatcher.AddObserver<object>(object,object,LF.Framework.EventCallback,object)
		// System.Void LF.Framework.EventDispatcher.PostEvent<object>(object,object,object[])
		// bool LF.Framework.EventDispatcher.RemoveObserver<object>(object,object,object)
		// LF.Framework.ResourceHandle LF.Framework.IResourceLoader.LoadAsset<object>(string)
		// System.Void LF.Framework.IResourceLoader.LoadAssetAsync<object>(string,System.Action<LF.Framework.ResourceHandle>)
		// object LF.Framework.ResourceManager.LoadAsset<object>(string)
		// System.Void LF.Framework.ResourceManager.LoadAssetAsync<object>(string,System.Action<object>)
		// object[] System.Array.Empty<object>()
		// System.Void System.Array.Resize<LF.GameLogic.CleanupTileMap>(LF.GameLogic.CleanupTileMap[]&,int)
		// System.Void System.Array.Resize<LF.GameLogic.MyRoomUI.ChatMsg>(LF.GameLogic.MyRoomUI.ChatMsg[]&,int)
		// object& System.Runtime.CompilerServices.Unsafe.As<object,object>(object&)
		// System.Void* System.Runtime.CompilerServices.Unsafe.AsPointer<object>(object&)
		// object UnityEngine.Component.GetComponent<object>()
		// object UnityEngine.Component.GetComponentInChildren<object>()
		// object UnityEngine.Component.GetComponentInParent<object>()
		// object[] UnityEngine.Component.GetComponentsInChildren<object>()
		// object[] UnityEngine.Component.GetComponentsInChildren<object>(bool)
		// object UnityEngine.GameObject.AddComponent<object>()
		// object UnityEngine.GameObject.GetComponent<object>()
		// object[] UnityEngine.GameObject.GetComponentsInChildren<object>(bool)
		// object UnityEngine.Object.FindObjectOfType<object>()
		// object[] UnityEngine.Object.FindObjectsOfType<object>()
		// object UnityEngine.Object.Instantiate<object>(object)
		// object UnityEngine.Object.Instantiate<object>(object,UnityEngine.Transform)
		// object UnityEngine.Object.Instantiate<object>(object,UnityEngine.Transform,bool)
		// object[] UnityEngine.Resources.ConvertObjects<object>(UnityEngine.Object[])
		// object UnityEngine.Resources.GetBuiltinResource<object>(string)
		// YooAsset.AssetHandle YooAsset.ResourcePackage.LoadAssetAsync<object>(string,uint)
	}
}