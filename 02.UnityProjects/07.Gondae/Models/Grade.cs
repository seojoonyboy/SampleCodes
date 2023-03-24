using UnityEngine;
using System;
using ScottGarland;

public class Grade {
    public int current = 0;
	public int socializationLevel;
    public GradeItem currentGrade {
        get {
            return gradeList[current];
        }
    }
    public string currentGradeName {
        get {
            return gradeList[current].name;
        }
    }
    public BigInteger upgradeCost {
        get {
            return gradeList[current+1].upgradeCost;
        }
    }
    public GradeItem[] gradeList;
    public GradeItem this[int _index]{
        get{
            return gradeList[_index];
        }
    }
	private Socialization[] socialization;
	public Socialization currentSocialization{
		get{
			return socialization[socializationLevel];
		}
	}
	public Socialization nextSocialization{
		get{
			if(socializationLevel<socialization.Length - 1){
				return socialization[socializationLevel + 1];
			} else {
				return null;
			}
		}
	}
    public Socialization prevSocialization{
        get{
            if(socializationLevel > 0 ){
                return socialization[socializationLevel - 1];
            } else {
                return null;
            }
        }
    }

	public int maxGrade{
		get {
			return socialization[socializationLevel].maxGrade;
		}
	}

    public int[] minionsLvList{
        get{
            return Array.ConvertAll<GradeItem, int>(gradeList, g=>g.minionsLevel);
        }
    }

    public int minionsLvMax = 150;

    public Grade(){
        TextAsset data = Resources.Load("Datafiles/grade") as TextAsset;
        string[] lines = data.text.Trim().Split('\n');
        int len = lines.Length;
        gradeList = new GradeItem[len-1];
        for(int i=1; i<len; i++) {
            gradeList[i-1] = new GradeItem(lines[i].Split(','));
        }

        data = Resources.Load("Datafiles/socialization") as TextAsset;
		lines = data.text.Trim().Split('\n');
		len = lines.Length;
		socialization = new Socialization[len - 1];
		for (int i = 1; i < len; i++) {
			socialization[i-1] = new Socialization(lines[i].Split(';'));
		}
    }
    public Grade(string[] minionsLevelList):this(){
        int len = minionsLevelList.Length;
        for( int i=0; i<len; i++ ){
            gradeList[i].minionsLevel = int.Parse(minionsLevelList[i]);
        }
    }
}

public class GradeItem {
    public string name;
    public int minionsLevel;
    public BigInteger upgradeCost;
    public string desc;
    public int[] unlockSkills;
    public int passive;
    public int minionLvupSeed;
    public double minionLvupConst;
    public int passiveRunTimeSeed;
    public decimal minionLvupCost{
        get{
            double exponential = minionsLevel * minionLvupConst + 1;
            return (decimal)Math.Round(Math.Pow(minionLvupSeed, exponential));
        }
    }

    public float passiveRunTime{
        get{
            float ret = passiveRunTimeSeed * (1 - (minionsLevel - 1) * 0.005f);
            return (float)Math.Round(ret, 2);
        }
    }

    public float passiveRunTimeAdd(int addLevel, int addRate, int totalRate){
        float ret = passiveRunTimeSeed * (1 - ((minionsLevel*(addRate+100)/100) + addLevel - 1) * 0.005f);
        ret = ret*(100-totalRate)/100;
        return (float)Math.Round(ret, 2);
    }

    public GradeItem( string[] arg ){
        string[] tmp;
        name = arg[0].Trim();
        upgradeCost = new BigInteger(arg[1].Trim());
        desc = arg[2].Trim();
        tmp = arg[3].Split('|');
        unlockSkills = new int[tmp.Length];
        for (int i=0; i<tmp.Length; i++){
            unlockSkills[i] = int.Parse(tmp[i]);
        }
        minionLvupSeed = arg[4] != "" ? int.Parse(arg[4]) : 0;
        minionLvupConst = arg[5] != "" ? double.Parse(arg[5]) : 0f;
        passiveRunTimeSeed = arg[6] != "" ? int.Parse(arg[6]) : 0;
    }
}

public class Socialization {
	public int cost;
	public int maxGrade;
    public string dialog;
    public string[] answer = new string[2];
    public string result;
	public Socialization( string[] arg ){
		cost = int.Parse(arg[1]);
		maxGrade = int.Parse(arg[3]);
		dialog = arg[2];
        answer[0] = arg[4];
        answer[1] = arg[5];
        result = arg[6];
	}
}