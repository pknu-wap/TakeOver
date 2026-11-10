using System;
using System.IO;
using System.Linq;
using TakeOver.Events;
using TakeOver.Story;
using UnityEditor;
using UnityEngine;

public static class TutorialVariantVerification
{
    public static void Run()
    {
        const string output="../QA_TutorialEventVariant/art-results.txt";
        Directory.CreateDirectory("../QA_TutorialEventVariant");File.WriteAllText(output,"");
        var original=AssetDatabase.LoadAssetAtPath<EventDefinition>("Assets/EventSystem/Data/InsideInfo.asset");
        var variant=Resources.Load<EventDefinition>("Story/TutorialVisit");
        void Check(bool value,string text){File.AppendAllText(output,(value?"PASS: ":"FAIL: ")+text+"\n");if(!value)throw new Exception(text);}
        Check(variant.getId()!=original.getId() && variant.IsTutorialOnly,"Independent tutorial identity; original main event retained");
        Check(variant.getSpeakerNpcId()==original.getSpeakerNpcId() && StoryDirector.TutorialContact==original.getSpeakerNpcId(),"Original CFO identity shared by event and memory guide");
        Check(variant.getImage()==original.getImage(),"Original detail image reused exactly");
        Check(variant.getCardImage()==original.getCardImage(),"Original card image reused exactly");
        Check(variant.getNegotiationBackground()==original.getNegotiationBackground(),"Original negotiation background setting reused exactly");
        Check(variant.getApCost()==original.getApCost() && variant.getChoices().Count==3,"Original interaction shape and AP cost retained");
        Check(variant.getConditions().Count==0,"No equity gate introduced into tutorial");
        Check(variant.getChoices().All(c=>c.getCashDelta()==0 && c.getNpcResult().targetNpcIds.All(id=>id==StoryDirector.FinancialDirector)),"Observation results affect only original CFO without payment");
        File.AppendAllText(output,"COMPLETE\n");NarrativeRevisionVerification.Run();
    }
}
