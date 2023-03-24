using UnityEngine;
using System;


public class Dialogues : ScriptableObject {
    public Dialogue[] hero;
    public Dialogue[] underHero;
    public Dialogue[] overHero;
    
    // private uint rotationCounter = 0;

    public string getDialogue(string who, int grade){
        Dialogue dig = null;
        switch (who)
        {
            case "hero":
                dig = hero[grade];
                break;
            case "underHero":
                dig = underHero[grade];
                break;
            case "overHero":
                dig = overHero[grade];
                break;
        }
        if(dig.dialogueList.Length == 0){
            return null;
        }

        var c = dig.rotationCounter % dig.dialogueList.Length;
        dig.rotationCounter++;
        if(dig.rotationCounter == uint.MaxValue){
            dig.rotationCounter = 0;
        }
        return dig.dialogueList[c];
    }
}

[Serializable]
public class Dialogue {
    public string[] dialogueList;
    public uint rotationCounter = 0;
}