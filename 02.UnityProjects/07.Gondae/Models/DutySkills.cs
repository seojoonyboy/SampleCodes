using UnityEngine;
using ScottGarland;
using System;

public class DutySkills {
    public BigInteger gdrSummary{
        get {
            BigInteger ret = new BigInteger(0);
            foreach(DutySkill s in skills) {
                ret = ret + s.touch;
            }
            return ret;
        }
    }
    public DutySkill[] skills;
    public DutySkill this[int _index]{
        get {
            return skills[_index];
        }
    }
    public int[] skillsLevelList{
        get {
            return Array.ConvertAll<DutySkill, int>(skills, s=>s.level);
        }
    }
    public DutySkills() {
        TextAsset data = Resources.Load("Datafiles/duty") as TextAsset;
        string[] lines = data.text.Trim().Split('\n');
        int len = lines.Length;
        skills = new DutySkill[len-1];
        for(int i=1; i<len; i++){   // 0번째 줄은 csv파일의 헤더라 무시한다.
            string l = lines[i];
            skills[i-1] = new DutySkill(l.Split(','));
        }
    }

    public DutySkills(int[] savedata):this(){
        int len = savedata.Length;
        for( int i=0; i<len; i++){
            skills[i].level = savedata[i];
        }
    }
}

public class DutySkill {
    public string name;
    public int level = 0;
    public int touchSeed;
    public int upgradeCostSeed;
    public bool isUpgradable = false;
    public Coef touchCoef;
    public Coef upgradeCoef;
    public BigInteger touch {
        get {
            return _operateTouch();
        }
    }
    public BigInteger nextTouch {
        get {
            return _operateTouch(1);
        }
    }
    public BigInteger upgradeCost {
        get {
            if(level == 0) {
                return new BigInteger(upgradeCostSeed);
            } else {
                double b = Math.Pow(upgradeCostSeed, level * upgradeCoef.a + upgradeCoef.b);
                return new BigInteger(b.ToString().Split('.')[0]);
            }
        }
    }

    public DutySkill(string[] arg){
        name = arg[0];
        touchSeed = int.Parse(arg[1]);
        touchCoef = new Coef(float.Parse(arg[2]), float.Parse(arg[3]));
        upgradeCostSeed = int.Parse(arg[4]);
        upgradeCoef = new Coef(float.Parse(arg[5]), float.Parse(arg[6]));
    }

    private BigInteger _operateTouch(int levelAdd = 0){
        int level = this.level + levelAdd;
        if(level == 0){
            return new BigInteger(0);
        } else if(level == 1){
            return new BigInteger(touchSeed);
        } else {
            double b = (touchCoef.a * level + touchCoef.b) * touchSeed;
            return new BigInteger(touchSeed + (int)b);
        }
    }
}

public struct Coef {
    public float a, b;
    public Coef (float coef1, float coef2){
        a = coef1;
        b = coef2;
    }
}
