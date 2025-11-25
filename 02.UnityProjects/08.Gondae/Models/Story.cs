using UnityEngine;
using System.Collections.Generic;
using System;

public class Story : ScriptableObject{
	public Chapter[] chapters;
	StoryCondition[] _allUnplayedConditions;
	private Dictionary<string, StoryCondition> minConditions = new Dictionary<string, StoryCondition>();
	void OnEnable(){
		foreach(var ch in chapters){
			foreach(var con in ch.conditions){
				con.parent = ch;
			}
		}
		var temp = new List<StoryCondition>();
		foreach(Chapter ch in chapters){
			if(ch.isPlayed){
				continue;
			}
			temp.AddRange(ch.conditions);
		}
		_allUnplayedConditions = temp.ToArray();
	}
	
	public Chapter getChepterBySocLv(){
		var matchChapters = Array.FindAll<Chapter>(chapters, c=> !c.isPlayed && c.subChapter==0);
		Array.Sort(matchChapters, (ch1,ch2)=>ch1.chapterNum - ch2.chapterNum);
		return matchChapters[0];
	}

	public Chapter getChapterByTypeMinValue(string type){
		if(!minConditions.ContainsKey(type) || minConditions[type].parent.isPlayed){
			var matchConditions = Array.FindAll<StoryCondition>(_allUnplayedConditions, c => c.type==type && c.parent.isPlayed==false );
			Array.Sort(matchConditions, (con1,con2)=>con1.val.CompareTo(con2.val));
			if(matchConditions.Length==0){
				return null;
			}
			minConditions[type] = matchConditions[0];
		}

		return minConditions[type].parent;
	}

	public Chapter[] getChaptersByGrade(){
		var matchConditions = Array.FindAll<StoryCondition>(_allUnplayedConditions, c => c.type=="grade" && c.parent.isPlayed==false);
		return Array.ConvertAll<StoryCondition, Chapter>(matchConditions, con=>con.parent);
	}

	public bool[] chapterIsPlayedList{
		set{
			for(var i=0; i<chapters.Length; i++){
				chapters[i].isPlayed = value[i];
			}
		}
		get{
			return Array.ConvertAll<Chapter, bool>(chapters, c=>c.isPlayed);
		}
	}
}

[Serializable]
public class Chapter {
	public int chapterNum;
	public int subChapter;
	[NonSerialized]
	public bool isPlayed = false;
	public StoryCondition[] conditions;
	public TextAsset playStoryContent(){
		string filename = chapterNum + "-" + subChapter;
		return Resources.Load("Chapters/Contents/"+filename) as TextAsset;
	}
}

[Serializable]
public class StoryCondition {
	public string type;
	public ulong val;
	[NonSerialized]
	public Chapter parent;
	public StoryCondition(string t, ulong v){
		type = t;
		val = v;
	}
}