using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LearningDialogView : MonoBehaviour
{
    public TMP_Text MethodText, TopicText, PlayerPositionText, OpponentPositionText;
    public TMP_Text PlayerHealthText, OpponentHealthText, OpponentText, HintText, FeedbackText, TranscriptText, StatusText, ProgressText;
    public Button StartButton, RecordButton, RetryButton, BackButton;

    public void ShowContext(string method, GameSession session)
    {
        MethodText.text = "Учимся применять: " + method;
        TopicText.text = session.TopicDescription;
        PlayerPositionText.text = "Ваша позиция\n" + session.PlayerPosition;
        OpponentPositionText.text = "Позиция оппонента\n" + session.OpponentPosition;
        ShowHealth(session);
    }

    public void ShowHealth(GameSession session)
    {
        PlayerHealthText.text = "Ваши HP: " + session.PlayerHealth;
        OpponentHealthText.text = "HP оппонента: " + session.OpponentHealth;
    }

    public void Controls(bool busy, bool ready, bool recording, bool canRecord, bool canRetry, bool finished)
    {
        RecordButton.interactable = !busy && ready && canRecord;
        RecordButton.GetComponentInChildren<TMP_Text>().text = recording ? "Закончить ответ" : "Ответить голосом";
        RetryButton.gameObject.SetActive(canRetry);
        RetryButton.interactable = !busy;
        StartButton.interactable = !busy && !recording;
        StartButton.GetComponentInChildren<TMP_Text>().text = finished ? "Ещё тренировка" : "Новая тренировка";
    }

    public void ShowReply(LearningDialogCoach.Reply reply, bool opening)
    {
        if (!reply.needsClarification)
        {
            OpponentText.text = "Оппонент\n" + reply.opponentReply;
            HintText.text = "Подсказка для следующей реплики\n" + reply.hint;
        }
        if (!opening)
        {
            TranscriptText.text = "Как понял тренер\n" + reply.interpretation;
            FeedbackText.text = reply.needsClarification ? "Уточни мысль\n" + reply.feedback :
                "Разбор: " + reply.playerCategory + "\n" + reply.feedback + "\nМожно сказать так: " + reply.improvedExample;
        }
    }
}
