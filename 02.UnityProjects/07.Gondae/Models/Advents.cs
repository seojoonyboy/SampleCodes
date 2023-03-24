using UnityEngine;
using System;

public class Advents {
    public Advent[] list;
    public static int lvLimit = 100;
    public decimal passiveSummary{
        get{
            decimal sum = 0;
            for(int i=0; i<list.Length; i++){
                sum += list[i].passive;
            }
            return sum;
        }
    }
    public int[] levels{
        get{
            return Array.ConvertAll<Advent,int>(list,adv=>adv.level);
        }
    }

    public Advents():this(Resources.Load("Datafiles/advent") as TextAsset){ }

    public Advents(TextAsset data){
        string[] rawLine = data.text.Trim().Split('\n');
        int len = rawLine.Length;
        list = new Advent[len-1];
        for(int i=1; i<len; i++) {
            list[i-1] = new Advent(rawLine[i].Split(','));
        }
    }

    public Advents(int[] levelList):this(Resources.Load("Datafiles/advent") as TextAsset){
        for(int i=0; i<list.Length; i++){
            list[i].level = levelList[i];
        }
    }
}

public class Advent{
    public string name;
    public decimal passiveBase;
    private decimal passiveCon1;
    private decimal costBase;
    private decimal costCon1;
    public int level;

    public decimal passive {
        get {
            if(level == 0) {
                return 0;
            }
            return passiveBase*(2+passiveCon1*level); 
        }
    }

    public decimal cost {
        get{
            return costBase*(1+costCon1*level);
        }
    }

    public Advent(string[] arg){
        name = arg[0];
        passiveBase = decimal.Parse(arg[1]);
        passiveCon1 = decimal.Parse(arg[2]);
        costBase = decimal.Parse(arg[3]);
        costCon1 = decimal.Parse(arg[4]);
    }
}
