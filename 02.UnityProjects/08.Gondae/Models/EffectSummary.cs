using System.Collections.Generic;

public class EffectSummary {
    public int criticalChance;  // 극대화 확률 % *
    public int healthful;       // 터치 시 체력 소모 없음 *
    public int minionAdd;       // 모든 등급의 부하직원의 레벨 증가
    public int minionAddRate;   // 부하직원 레벨 상승 %
    public int passiveGDR;      // 패시브 곤대력 추가 % *
    public int touchGDR;        // 터치 곤대력 추가 % *
    public int healthIncrease;  // 체력 증가 *
    public int passiveSpeed;    // 패시브 실행 속도 감소 %
    
    public int this[string propertyName]{
        get {
            switch(propertyName){
            case "criticalChance":
                return criticalChance;
            case "healthful":
                return healthful;
            case "minionAdd":
                return minionAdd;
            case "minionAddRate":
                return minionAddRate;
            case "passiveGDR":
                return passiveGDR;
            case "touchGDR":
                return touchGDR;
            case "healthIncrease":
                return healthIncrease;
            case "passiveSpeed":
                return passiveSpeed;
            default:
                return 0;
            }
        }
        set {
            switch(propertyName){
            case "criticalChance":
                criticalChance = value; break;
            case "healthful":
                healthful = value; break;
            case "minionAdd":
                minionAdd = value; break;
            case "minionAddRate":
                minionAddRate = value; break;
            case "passiveGDR":
                passiveGDR = value; break;
            case "touchGDR":
                touchGDR = value; break;
            case "healthIncrease":
                healthIncrease = value; break;
            case "passiveSpeed":
                passiveSpeed = value; break;
            default:
                break;
            }
        }
    }

    public void init(){
        criticalChance = 0;
        healthful = 0;
        minionAdd = 0;
        minionAddRate = 0;
        passiveGDR = 0;
        touchGDR = 0;
        healthIncrease = 0;
        passiveSpeed = 0;
    }

    public override string ToString(){
        Dictionary<string, string> mapString = new Dictionary<string, string>();
        mapString.Add("criticalChance", "곤대력 극대화 확률 {0:d}% 상승");
        mapString.Add("healthful", "터치 시 체력 소모 없음");
        mapString.Add("minionAdd", "부하직원의 수 {0:d}명 증가");
        mapString.Add("minionAddRate", "부하직원의 수 {0:d}% 증가");
        mapString.Add("passiveGDR", "패시브 곤대력 획득량 {0:d}% 증가");
        mapString.Add("touchGDR", "터치 곤대력 획득량 {0:d}% 상승");
        mapString.Add("healthIncrease", "체력 {0:d}% 증가");
        mapString.Add("passiveSpeed", "패시브 곤대력 획득 시간 {0:d}% 감소");
        string output = "";

        foreach(KeyValuePair<string, string> kv in mapString){
            if(this[kv.Key] > 0){
                output += string.Format(kv.Value + "\n", this[kv.Key]);
            }
        }

        return output;
    }
}