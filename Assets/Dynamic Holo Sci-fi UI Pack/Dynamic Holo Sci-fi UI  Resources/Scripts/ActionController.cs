using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;

public class ActionController : MonoBehaviour
{
	public static ActionController Instance;
	private void Awake()
	{
		Instance = this;
	}
	//延时事件流
	public static Dictionary<Action, float> actionDic = new Dictionary<Action, float>();
	public void RigisterAction(Action action, float time)
	{
		actionDic.Add(action, time);
	}
	public void ClearActions()
	{
		actionDic.Clear();
	}
	public void CallActions()
	{
		List<Action> theActionList = new List<Action>(actionDic.Keys);
		List<float> theTimeList = new List<float>(actionDic.Values);
		theActionList[0]();
		for (int i = 1; i < theActionList.Count; i++)
		{
			StartCoroutine(DelayToCallIEnumerator(theActionList[i], Sum(theTimeList, i - 1)));
		}
	}
	public void CallActions(bool IsClear)
	{
		CallActions();
		if (IsClear)
		{
			ClearActions();
		}
	}
	float Sum(List<float> list, int index)
	{
		float x = 0;
		for (int i = 0; i < list.Count; i++)
		{
			if (i <= index)
			{
				x += list[i];
			}
		}
		return x;
	}

	public void DelayToCall(Action action, float time)
	{
		StartCoroutine(DelayToCallIEnumerator(action, time));
	}

	private IEnumerator DelayToCallIEnumerator(Action action, float time)
	{

		yield return new WaitForSeconds(time);
		action();
	}


}