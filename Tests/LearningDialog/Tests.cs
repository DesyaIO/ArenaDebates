using System;
using System.Reflection;
class Program
{
 static int count;
 static void Check(bool value,string name){if(!value)throw new Exception(name);count++;}
 static void Call(object x,string name,params object[] args)=>x.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(x,args);
 static T Get<T>(object x,string name)=>(T)x.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(x);
 static void Main(){
  var types=new AnswerTypeDatabaseSO();types.Types.Add(new AnswerTypeSO{TypeName="SPIN",DamageToOpponent=-20});
  var client=new DeepSeekClient();var coach=new LearningDialogCoach{Client=client,AnswerTypes=types};
  string reply="{\"playerCategory\":\"SPIN\",\"interpretation\":\"Ваш вопрос\",\"feedback\":\"Хороший вопрос\",\"improvedExample\":\"Как это влияет на вас?\",\"needsClarification\":false,\"methodApplied\":true,\"opponentReply\":\"Моя позиция\",\"opponentCategory\":\"SPIN\",\"hint\":\"Задайте вопрос о последствиях\"}";
  Check(coach.TryParse(reply,false,out _,out _),"valid structured response");
  Check(coach.TryParse("```json\n"+reply+"\n```",false,out _,out _),"fenced JSON");
  Check(!coach.TryParse("{}",false,out _,out _),"empty response rejected");
  Check(!coach.TryParse("{broken",false,out _,out _),"malformed response rejected");
  Check(!coach.TryParse(reply.Replace("SPIN","unknown"),false,out _,out _),"unknown category rejected");
  string clarify="{\"needsClarification\":true,\"feedback\":\"Повторите мысль\"}";
  Check(coach.TryParse(clarify,false,out _,out _),"clarification accepted without damage categories");
  var view=new LearningDialogView{MethodText=new(),TopicText=new(),PlayerPositionText=new(),OpponentPositionText=new(),PlayerHealthText=new(),OpponentHealthText=new(),OpponentText=new(),HintText=new(),FeedbackText=new(),TranscriptText=new(),StatusText=new(),ProgressText=new(),StartButton=new(),RecordButton=new(),RetryButton=new(),BackButton=new()};
  var topic=new DebateTopicSO{Description="topic"};topic.Positions.Add(new PositionSO{Description="A"});topic.Positions.Add(new PositionSO{Description="B"});
  var controller=new LearningDialogController{View=view,Coach=coach,Speech=new(),Topics=new[]{topic},Lessons=Array.Empty<LearningContentSO>()};
  Call(controller,"Awake");Call(controller,"Start");controller.StartLesson();controller.StartLesson();Check(client.Requests.Count==1,"duplicate start blocked");
  Call(controller,"SpeechStatus","Initialized");client.Requests[0].ok(reply);
  var session=Get<GameSession>(controller,"_session");Check(session.IsPlayerTurn&&session.PlayerHealth==100,"opening reply does not damage learner");Check(view.RecordButton.interactable,"record enabled after opening");
  Call(controller,"Record");Call(controller,"Record");Call(controller,"Transcript","речь");Call(controller,"Transcript","duplicate");Check(client.Requests.Count==2,"one request per recording");
  client.Requests[1].ok(clarify);Check(session.Entries.Count==1&&session.PlayerHealth==100,"clarification preserves history and health");
  for(int turn=0;turn<3;turn++){
   Call(controller,"Record");Call(controller,"Record");Call(controller,"Transcript","my words");
   var req=client.Requests[client.Requests.Count-1];req.ok(reply);req.ok(reply);
   Check(Get<int>(controller,"_turns")==turn+1,"one accepted turn per callback");
  }
  Check(Get<bool>(controller,"_finished")&&!view.RecordButton.interactable,"three replies complete practice");
  Check(session.Entries.Count==7&&session.PlayerHealth==100&&session.OpponentHealth==100,"training replies do not change legacy health");
  foreach(int limit in new[]{5,10}) foreach(bool first in new[]{true,false}) {
   var dialog=new GameSession(topic,topic.Positions[0],topic.Positions[1],first){TurnsPerParticipant=limit};
   for(int i=0;i<limit*2;i++) {
    bool player=dialog.IsPlayerTurn;
    Check(dialog.AddEntry(!player,"wrong turn","SPIN","",-15)==null,"out of turn rejected");
    Check(dialog.AddEntry(player,"reply","SPIN","analysis",-15)!=null,"accepted alternating turn");
    Check(dialog.AddEntry(player,"duplicate","SPIN","",-15)==null,"duplicate rejected");
    Check(dialog.IsGameOver==(i==limit*2-1),"ends exactly at turn limit");
   }
   Check(dialog.PlayerTurns==limit&&dialog.OpponentTurns==limit,"equal turns for either starting speaker");
   Check(dialog.PlayerHealth==100&&dialog.OpponentHealth==100,"dialog does not change health");
   Check(dialog.RollbackTo(limit*2-1)&&!dialog.IsGameOver,"rollback reopens completed dialog");
   Check(dialog.AddEntry(dialog.IsPlayerTurn,"replayed","SPIN","",0)!=null&&dialog.IsGameOver,"replayed final turn completes dialog");
  }
  Check(controller.Active.Count==0,"no leftover processing animation");
  controller.StartLesson();int index=client.Requests.Count-1;client.Requests[index].error("network");Check(view.RetryButton.gameObject.activeSelf&&!Get<bool>(controller,"_busy"),"manual retry after error");Call(controller,"Retry");Check(client.Requests.Count==index+2,"retry sends exactly one request");
  Call(controller,"Cancel");client.Requests[client.Requests.Count-1].ok(reply);Check(Get<GameSession>(controller,"_session").Entries.Count==0,"late response ignored after cancel");
  string prompt=coach.BuildPrompt("SPIN",null,session,"неточная речь");Check(prompt.Contains("неточная речь")&&prompt.Contains("Не добавляй отсутствующие аргументы")&&prompt.Contains("SPIN"),"speech tolerance and chosen method included");
  Console.WriteLine($"PASS: {count} learning regression checks");
 }
}
