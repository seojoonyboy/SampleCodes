using UnityEngine;
using System.Collections.Generic;
using System;

public class Line : ScriptableObject {
    public LineItem[] serializableLineList;
    public List<LineItem>[] lineList;
    private EffectSummary linePassiveSummary = new EffectSummary();
    public int linePoint;

    public EffectSummary summary {
        get {
            summaryPassive();
            return linePassiveSummary;
        }
    }

    public int lastLevelIndex{
        get {
            int i = 0;
            for (; i<lineList.Length; i++){
                List<LineItem> li = lineList[i];
                bool upgraded = li.TrueForAll((l) => l.isUpgraded == false);
                if(upgraded){
                    break;
                }
            }
            return i;
        }
    }

    void OnEnable(){
        Dictionary<int, List<LineItem>> tempDict = new Dictionary<int, List<LineItem>>();
        foreach(var li in serializableLineList){
            var dictKey = li.idx;
            if(tempDict.ContainsKey(dictKey)){
                tempDict[dictKey].Add(li);
            } else {
                List<LineItem> tempList = new List<LineItem>();
                tempList.Add(li);
                tempDict.Add(dictKey, tempList);
            }
            var len = tempDict.Count;
            lineList = new List<LineItem>[len];
            for(int i=0; i<len; i++) {
                lineList[i] = tempDict[i];
            }
        }
    }

    public void loadLine(int[] upgradedInfo, int linePointData){
        foreach(var l in serializableLineList){
            l.isUpgraded = false;
        }
        linePoint = linePointData;
        if(upgradedInfo == null) return;        
        int len = upgradedInfo.Length;
        linePassiveSummary.init();
        for(int i=0; i<len; i++){
            int upgraded = upgradedInfo[i];
            lineList[i][upgraded].isUpgraded = true;
            addPassive(i, upgraded);
        }
    }

    public int[] upgradedInfo{
        get {
            List<int> temp = new List<int>();
            foreach(List<LineItem> list in lineList){
                int i = list.FindIndex(l => l.isUpgraded);
                if(i<0){
                    break;
                } else {
                    temp.Add(i);
                }
            }
            return temp.ToArray();
        }
    }

    private void summaryPassive(){
        int len = upgradedInfo.Length;
        linePassiveSummary.init();
        for(int i=0; i<len; i++){
            int upgraded = upgradedInfo[i];
            addPassive(i, upgraded);
        }
    }

    private void addPassive(int listIndex, int itemIndex){
        LineItem item = lineList[listIndex][itemIndex];
        if(item.effectType == EffectType.PASSIVE){
            linePassiveSummary[item.passiveField] += item.passiveValue;
        }
    }
}

public enum EffectType { PASSIVE, ACTIVE };

[Serializable]
public class LineItem {
    public int idx;
    public string name;
    public EffectType effectType;
    public int activeSkillID = -1;
    public string passiveField = null;
    public int passiveValue = 0;
    public string desc;
    [NonSerialized]
    public bool isUpgraded;

    public LineItem(string[] arr){
        idx = int.Parse(arr[0]);
        name = arr[1];
        desc = arr[6];
        if(arr[2] == "PASSIVE"){
            effectType = EffectType.PASSIVE;
            passiveField = arr[4];
            passiveValue = int.Parse(arr[5]);
        } else {
            effectType = EffectType.ACTIVE;
            activeSkillID = int.Parse(arr[3]);
        }
        isUpgraded = false;
    }
}
