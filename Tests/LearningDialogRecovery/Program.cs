using System;
using System.Reflection;
using UnityEngine;

static class Program
{
    private static int checks;
    private const string Reply = """
        {"playerCategory":"SPIN","interpretation":"Ваш вопрос","feedback":"Хороший вопрос",
        "improvedExample":"Как это влияет на вас?","needsClarification":false,"methodApplied":true,
        "opponentReply":"Моя позиция","opponentCategory":"SPIN","hint":"Уточните последствия"}
        """;
    private static void Check(bool value, string description)
    { if (!value) throw new Exception(description); checks++; }
    private static void Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static T Field<T>(object target, string field) => (T)target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static LearningDialogController Create()
    {
        Time.realtimeSinceStartup = 0;
        var types = new AnswerTypeDatabaseSO();
        types.Types.Add(new AnswerTypeSO { TypeName = "SPIN" });
        var topic = new DebateTopicSO { Description = "Тема" };
        topic.Positions.Add(new PositionSO { Description = "A" });
        topic.Positions.Add(new PositionSO { Description = "B" });
        var controller = new LearningDialogController
        {
            View = new(), Speech = new(), Topics = new[] { topic }, Lessons = Array.Empty<LearningContentSO>(),
            Coach = new() { Client = new(), AnswerTypes = types }
        };
        Call(controller, "Awake"); Call(controller, "Start");
        controller.StartLesson(); Call(controller, "SpeechStatus", "Initialized");
        return controller;
    }
    private static void Submit(LearningDialogController controller, string text)
    {
        Call(controller, "Record"); Call(controller, "Record");
        Call(controller, "Transcript", text); Call(controller, "SubmitTranscript");
    }
    private static void CheckRecoverable(LearningDialogController controller, int entries, int turns)
    {
        Check(!controller.View.Busy && controller.View.CanRetry, "failure unlocks manual retry");
        Check(controller.Active.Count == 0, "failure stops progress animation");
        Check(Field<Coroutine>(controller, "_request") == null, "failure clears request handle");
        Check(Field<GameSession>(controller, "_session").Entries.Count == entries, "failure preserves history");
        Check(Field<int>(controller, "_turns") == turns, "failure does not consume a turn");
        string screen = controller.View.Screen;
        Call(controller, "ContinueAfterFeedback");
        Check(controller.View.Screen == screen, "cannot continue without an accepted reply");
    }
    private static void Main()
    {
        foreach (bool networkError in new[] { false, true })
        {
            var controller = Create();
            var client = controller.Coach.Client;
            var opening = client.Requests[0];
            if (networkError) opening.Error("connection reset"); else opening.Ok("{}");
            CheckRecoverable(controller, 0, 0);
            Check(client.Requests.Count == 1, "no automatic paid retries");
            Call(controller, "Retry"); Call(controller, "Retry");
            Check(client.Requests.Count == 2, "opening retry works without a player transcript, only once");
            Check(client.Requests[1].Prompt == opening.Prompt, "opening retry preserves context");
            opening.Ok(Reply);
            Check(Field<GameSession>(controller, "_session").Entries.Count == 0, "late old callback ignored");
            client.Requests[1].Ok(Reply); client.Requests[1].Ok(Reply);
            Check(Field<GameSession>(controller, "_session").Entries.Count == 1, "opening accepted once");
            Check(!controller.View.Busy && !controller.View.CanRetry, "success leaves recovery state");

            Submit(controller, "Моя сохранённая реплика");
            var failed = client.Requests[^1];
            failed.Ok("{\"feedback\":\"обрыв");
            CheckRecoverable(controller, 1, 0);
            Check(Field<string>(controller, "_submittedTranscript") == "Моя сохранённая реплика", "transcript survives invalid JSON");
            Call(controller, "Retry");
            Check(client.Requests[^1].Prompt == failed.Prompt, "analysis retry preserves exact prompt");
            client.Requests[^1].Error("VPN disconnected");
            CheckRecoverable(controller, 1, 0);
            Call(controller, "Retry");
            var accepted = client.Requests[^1]; accepted.Ok(Reply); accepted.Ok(Reply); accepted.Error("late error");
            Check(Field<int>(controller, "_turns") == 1, "recovered answer counted once");
            Check(Field<GameSession>(controller, "_session").Entries.Count == 3, "one player/opponent pair after recovery");
            Check(!controller.View.CanRetry, "late error cannot undo a successful response");
            Check(client.Requests.TrueForAll(request => request.Warnings), "recoverable requests opt out of error-pause logs");
        }

        var timed = Create();
        var expired = timed.Coach.Client.Requests[0];
        Time.realtimeSinceStartup = 126; Call(timed, "Update");
        Check(expired.Handle.Stopped, "timeout cancels the hanging coroutine");
        CheckRecoverable(timed, 0, 0);
        Call(timed, "Retry"); expired.Ok(Reply);
        Check(Field<GameSession>(timed, "_session").Entries.Count == 0, "late timed-out response ignored");
        timed.Coach.Client.Requests[^1].Ok(Reply);
        Check(Field<GameSession>(timed, "_session").Entries.Count == 1, "timeout recovery succeeds");
        Submit(timed, "Ещё реплика");
        var abandoned = timed.Coach.Client.Requests[^1];
        Call(timed, "Cancel"); abandoned.Ok(Reply);
        Check(Field<GameSession>(timed, "_session").Entries.Count == 1, "cancel ignores late result");
        Check(timed.Active.Count == 0, "cancel stops progress animation");

        var synchronous = Create();
        synchronous.Coach.Client.Requests[0].Error("network");
        synchronous.Coach.Client.FailSynchronously = true;
        Call(synchronous, "Retry");
        CheckRecoverable(synchronous, 0, 0);
        Console.WriteLine($"PASS: {checks} LearningDialog recovery checks (no network requests).");
    }
}
