using UnityEngine;
using System;

public class BuffSkills : ScriptableObject {
    public BuffSkill[] skills;
    private EffectSummary buffSummary = new EffectSummary();
    public BuffSkill this[int index]{
        get {
            return skills[index];
        }
    }
    public bool[] unlockedMap{
        get {
            return Array.ConvertAll<BuffSkill, bool>(skills,b=>b.isUnlocked); 
        }
    }

    public int[] remainTime{
        get{
            return Array.ConvertAll<BuffSkill, int>(skills, (bf)=>{
                if(bf.isActivated == false){
                    return 0;
                } else {
                    var t = DateTime.Now.Subtract(bf.activateTime);
                    return bf.duration - (int)t.TotalSeconds;
                }
            });
        }
    }
    
    public EffectSummary summary{
        get {
            BuffSkill[] activatedSkills = Array.FindAll(skills,b=>b.isActivated);
            buffSummary.init();
            foreach(BuffSkill s in activatedSkills){
                foreach(BuffSkillEffect e in s.effects){
                    buffSummary[e.field] += e.val;
                }
            }
            return buffSummary;
        }
    }
    public void loadBuffSkills(bool[] savedUnlock){
        if (savedUnlock == null) {
            foreach(var s in skills){
                s.isUnlocked = false;
                s.isActivated = false;
            }
            return;
        }
        int len = savedUnlock.Length;
        for(int i=0; i<len; i++){
            skills[i].isUnlocked = savedUnlock[i];
            skills[i].isActivated = false;
        }
    }
}

[Serializable]
public class BuffSkill{
    public string name;
    public BuffSkillEffect[] effects;
    public int duration;
    public int cost;
    public string desc;
    [NonSerialized]
    public bool isUnlocked;
    [NonSerialized]
    public bool isActivated;
    public DateTime activateTime;
    public BuffSkill(string[] arg){
        name = arg[0].Trim();
        effects = new BuffSkillEffect[1];
        effects[0] = new BuffSkillEffect(arg[1].Trim(), int.Parse(arg[2]));
        duration = int.Parse(arg[3]);
        cost = int.Parse(arg[4]);
        desc = arg[5].Trim();
        isUnlocked = false;
        isActivated = false;
    }
}

[Serializable]
public class BuffSkillEffect{
    public string field;
    public int val;
    public BuffSkillEffect(string f, int v){
        field = f;
        val = v;
    }
}
