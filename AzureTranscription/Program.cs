using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Transcription;

namespace AzureTranscription;

class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Transcribing:");
        await TranscribeConversationsAsync(args[0], args[1]);
    }

    public static async Task TranscribeConversationsAsync(string key, string path)
    {
        var config = SpeechConfig.FromSubscription(key, "westeurope");
        config.SetProperty("ConversationTranscriptionInRoomAndOnline", "true");

        var stopRecognition = new TaskCompletionSource<int>();

        using var audioInput = AudioConfig.FromWavFileInput(path);
        using var conversation = await Conversation.CreateConversationAsync(config, Guid.NewGuid().ToString());
        using var conversationTranscriber = new ConversationTranscriber(audioInput);

        conversationTranscriber.Transcribing += (s, e) =>
        {
            Console.WriteLine($"TRANSCRIBING: Text={e.Result.Text}");
        };

        conversationTranscriber.Transcribed += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                Console.WriteLine($"TRANSCRIBED: Text={e.Result.Text}");
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                Console.WriteLine($"NOMATCH: Speech could not be recognized.");
            }
        };

        conversationTranscriber.Canceled += (s, e) =>
        {
            Console.WriteLine($"CANCELED: Reason={e.Reason}");

            if (e.Reason == CancellationReason.Error)
            {
                Console.WriteLine($"CANCELED: ErrorCode={e.ErrorCode}");
                Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
                Console.WriteLine($"CANCELED: Did you update the subscription info?");
                stopRecognition.TrySetResult(0);
            }
        };

        conversationTranscriber.SessionStarted += (s, e) =>
        {
            Console.WriteLine($"\nSession started event. SessionId={e.SessionId}");
        };

        conversationTranscriber.SessionStopped += (s, e) =>
        {
            Console.WriteLine($"\nSession stopped event. SessionId={e.SessionId}");
            Console.WriteLine("\nStop recognition.");
            stopRecognition.TrySetResult(0);
        };

        // Join to the conversation and start transcribing
        await conversationTranscriber.JoinConversationAsync(conversation);
        await conversationTranscriber.StartTranscribingAsync().ConfigureAwait(false);

        // waits for completion, then stop transcription
        Task.WaitAny(new[] { stopRecognition.Task });
        await conversationTranscriber.StopTranscribingAsync().ConfigureAwait(false);
    }
}