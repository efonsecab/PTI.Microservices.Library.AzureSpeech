using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.PronunciationAssessment;
using Microsoft.CognitiveServices.Speech.Translation;
using Microsoft.Extensions.Logging;
using PTI.Microservices.Library.Configuration;
using PTI.Microservices.Library.Interceptors;
using PTI.Microservices.Library.Models.AzureSpeechService;
using PTI.Microservices.Library.Models.AzureSpeechService.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace PTI.Microservices.Library.Services
{
    /// <summary>
    /// Check samples here: https://github.com/Azure-Samples/cognitive-services-speech-sdk/tree/master/samples/csharp/sharedcontent/console
    /// </summary>
    public sealed class AzureSpeechService
    {
        private ILogger<AzureSpeechService> Logger { get; }
        private AzureSpeechConfiguration AzureSpeechConfiguration { get; }
        private CustomHttpClient CustomHttpClient { get; }
        private SpeechConfig SpeechConfig { get; }
        private SpeechTranslationConfig SpeechTranslationConfig { get; }


        /// <summary>
        /// Creates a new instance of <see cref="AzureSpeechService"/>
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="azureSpeechConfiguration"></param>
        /// <param name="customHttpClient"></param>
        public AzureSpeechService(ILogger<AzureSpeechService> logger, AzureSpeechConfiguration azureSpeechConfiguration,
            CustomHttpClient customHttpClient)
        {
            this.Logger = logger;
            this.AzureSpeechConfiguration = azureSpeechConfiguration;
            this.CustomHttpClient = customHttpClient;
            this.SpeechConfig =
                SpeechConfig.FromSubscription(this.AzureSpeechConfiguration.Key, this.AzureSpeechConfiguration.Region);
            this.SpeechConfig.OutputFormat = OutputFormat.Detailed;
            this.SpeechTranslationConfig =
                SpeechTranslationConfig.FromSubscription(this.AzureSpeechConfiguration.Key, this.AzureSpeechConfiguration.Region);
            this.SpeechTranslationConfig.OutputFormat = OutputFormat.Detailed;
        }

        /// <summary>
        /// Converts a text to audio using the SSML format and send it to the deffault speakers
        /// </summary>
        /// <param name="ssmlMessage"></param>
        /// <returns></returns>
        public async Task<SpeechSynthesisResult> TalkToDefaultSpeakersWithSSML(SSMLMessage ssmlMessage)
        {
            try
            {
                var testAudioConfig = AudioConfig.FromDefaultSpeakerOutput();
                SpeechSynthesizer synthesizer = new SpeechSynthesizer(this.SpeechConfig, testAudioConfig);
                XmlSerializerNamespaces xmlSerializerNamespaces = new XmlSerializerNamespaces(new[]
                {
                    XmlQualifiedName.Empty
                });
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(SSMLMessage));
                XmlWriterSettings xmlWriterSettings = new XmlWriterSettings()
                {
                    Indent = true,
                    OmitXmlDeclaration = true
                };
                string ssmlContents = string.Empty;
                using (StringWriter writer = new StringWriter())
                {
                    var xmlWriter = XmlWriter.Create(writer, xmlWriterSettings);
                    xmlSerializer.Serialize(xmlWriter, ssmlMessage, xmlSerializerNamespaces);
                    ssmlContents = writer.ToString();
                    writer.Close();
                }
                var result = await synthesizer.SpeakSsmlAsync(ssmlContents);
                if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                    throw new Exception($"Error creating audio. Error Code: {cancellation.ErrorCode} " +
                        $"Reason: {cancellation.Reason} - Details: {cancellation.ErrorDetails}");
                }
                return result;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Creates an audio stream from the specified text
        /// </summary>
        /// <param name="text"></param>
        /// <param name="locale"></param>
        /// <param name="voiceName"></param>
        /// <returns></returns>
        public async Task<(SpeechSynthesisResult, PullAudioOutputStream)> CreateAudioStreamAsync(string text, string locale = "en-US",
            string voiceName = "AriaNeural")
        {
            try
            {
                var voiceFullname = $"Microsoft Server Speech Text to Speech Voice ({locale}, {voiceName})";
                var outputStream = AudioOutputStream.CreatePullStream();
                this.SpeechConfig.SpeechSynthesisVoiceName = voiceFullname;
                var testAudioConfig = AudioConfig.FromStreamOutput(outputStream);
                SpeechSynthesizer synthesizer = new SpeechSynthesizer(this.SpeechConfig, testAudioConfig);
                var result = await synthesizer.SpeakTextAsync(text);
                if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                    throw new Exception($"Error creating audio. Error Code: {cancellation.ErrorCode} " +
                        $"Reason: {cancellation.Reason} - Details: {cancellation.ErrorDetails}");
                }
                return (result, outputStream);
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets text from an audio in WAV format
        /// </summary>
        /// <param name="audioStream"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<string> GetTextFromAudioStreamAsync(Stream audioStream, CancellationToken cancellationToken = default)
        {
            try
            {
                //Check https://github.com/Azure-Samples/cognitive-services-speech-sdk/blob/master/samples/csharp/sharedcontent/console/speech_recognition_samples.cs
                //Using DEFAULT WAV foramt in order to avoid having dependencies on installed libraries
                // <recognitionWithCompressedInputPullStreamAudio>
                // Creates an instance of a speech config with specified subscription key and service region.
                // Replace with your own subscription key and service region (e.g., "westus").
                var config = this.SpeechConfig;
                StringBuilder strBuilder = new StringBuilder();

                var stopRecognition = new TaskCompletionSource<int>();

                // Create an audio stream from a wav file.
                // Replace with your own audio file name.

                using (var audioInput = AudioConfig.FromStreamInput(new PullAudioInputStream(new BinaryAudioStreamReader(
                                        new BinaryReader(audioStream)),
                                        AudioStreamFormat.GetDefaultOutputFormat())))
                {
                    // Creates a speech recognizer using audio stream input.
                    using (var recognizer = new SpeechRecognizer(config, audioInput))
                    {
                        // Subscribes to events.
                        recognizer.Recognizing += (s, e) =>
                        {
                            Console.WriteLine($"RECOGNIZING: Text={e.Result.Text}");
                        };

                        recognizer.Recognized += (s, e) =>
                        {
                            if (e.Result.Reason == ResultReason.RecognizedSpeech)
                            {
                                Console.WriteLine($"RECOGNIZED: Text={e.Result.Text}");
                                strBuilder.AppendLine(e.Result.Text);
                            }
                            else if (e.Result.Reason == ResultReason.NoMatch)
                            {
                                Console.WriteLine($"NOMATCH: Speech could not be recognized.");
                            }
                        };

                        recognizer.Canceled += (s, e) =>
                        {
                            Console.WriteLine($"CANCELED: Reason={e.Reason}");

                            if (e.Reason == CancellationReason.Error)
                            {
                                Console.WriteLine($"CANCELED: ErrorCode={e.ErrorCode}");
                                Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
                                Console.WriteLine($"CANCELED: Did you update the subscription info?");
                            }

                            stopRecognition.TrySetResult(0);
                        };

                        recognizer.SessionStarted += (s, e) =>
                        {
                            Console.WriteLine("\nSession started event.");
                        };

                        recognizer.SessionStopped += (s, e) =>
                        {
                            Console.WriteLine("\nSession stopped event.");
                            Console.WriteLine("\nStop recognition.");
                            stopRecognition.TrySetResult(0);
                        };

                        // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
                        await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

                        // Waits for completion.
                        // Use Task.WaitAny to keep the task rooted.
                        Task.WaitAny(new[] { stopRecognition.Task });

                        // Stops recognition.
                        await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
                        return strBuilder.ToString();
                    }
                }
                // </recognitionWithCompressedInputPullStreamAudio>
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Converts a text to audio and send it to the default speakers
        /// </summary>
        /// <param name="text"></param>
        /// <param name="locale"></param>
        /// <param name="voiceName"></param>
        /// <returns></returns>
        public async Task<SpeechSynthesisResult> TalkToDefaultSpeakersAsync(string text, string locale = "en-US",
            string voiceName = "AriaNeural")
        {
            try
            {
                var voiceFullname = $"Microsoft Server Speech Text to Speech Voice ({locale}, {voiceName})";
                this.SpeechConfig.SpeechSynthesisVoiceName = voiceFullname;
                var testAudioConfig = AudioConfig.FromDefaultSpeakerOutput();
                SpeechSynthesizer synthesizer = new SpeechSynthesizer(this.SpeechConfig, testAudioConfig);
                var result = await synthesizer.SpeakTextAsync(text);
                if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                    throw new Exception($"Error creating audio. Error Code: {cancellation.ErrorCode} " +
                        $"Reason: {cancellation.Reason} - Details: {cancellation.ErrorDetails}");
                }
                return result;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Converts text to audio and writes it to the specified output stream. Saves output in WAV format
        /// </summary>
        /// <param name="text"></param>
        /// <param name="outputStream"></param>
        /// <param name="locale"></param>
        /// <param name="voiceName"></param>
        /// <returns></returns>
        public async Task<SpeechSynthesisResult> TalkToStreamAsync(string text, Stream outputStream, string locale = "en-US",
            string voiceName = "AriaNeural")
        {
            try
            {
                AutoResetEvent autoResetEvent = new AutoResetEvent(false);
                //this.SpeechConfig.SetSpeechSynthesisOutputFormat();
                var voiceFullname = $"Microsoft Server Speech Text to Speech Voice ({locale}, {voiceName})";
                this.SpeechConfig.SpeechSynthesisVoiceName = voiceFullname;
                var pullStream = AudioOutputStream.CreatePullStream();
                var testAudioConfig = AudioConfig.FromStreamOutput(pullStream);
                SpeechSynthesizer synthesizer = new SpeechSynthesizer(this.SpeechConfig, testAudioConfig);
                synthesizer.SynthesisCompleted += async (sender, e) =>
                 {
                     var audioBytes = e.Result.AudioData;
                     await outputStream.WriteAsync(audioBytes, 0, audioBytes.Length);
                     autoResetEvent.Set();
                 };
                var result = await synthesizer.SpeakTextAsync(text);
                if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                    throw new Exception($"Error creating audio. Error Code: {cancellation.ErrorCode} " +
                        $"Reason: {cancellation.Reason} - Details: {cancellation.ErrorDetails}");
                }
                autoResetEvent.WaitOne();
                return result;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task TranslateContinuousSpeechAsync(string inputLanguage, 
            string outputLanguage, string outputVoiceName)
        {
            this.SpeechTranslationConfig.SpeechRecognitionLanguage = inputLanguage;
            this.SpeechTranslationConfig.AddTargetLanguage(outputLanguage);

            this.SpeechTranslationConfig.VoiceName = outputVoiceName;
            this.SpeechTranslationConfig.SpeechSynthesisVoiceName = outputVoiceName;
            this.SpeechConfig.SpeechSynthesisLanguage = outputLanguage;
            this.SpeechConfig.SpeechSynthesisVoiceName = outputVoiceName;
            using var synthetizer = new SpeechSynthesizer(this.SpeechConfig, AudioConfig.FromDefaultSpeakerOutput());
            using var recognizer = new TranslationRecognizer(this.SpeechTranslationConfig);
            var stopRecognition = new TaskCompletionSource<int>();
            recognizer.Recognizing += (sender, e) => { };
            recognizer.Recognized += async (sender, e) => 
            {
                if (e.Result.Reason == ResultReason.TranslatedSpeech)
                {
                    var text = e.Result.Translations.First().Value;
                    await synthetizer.SpeakTextAsync(text);
                }
            };
            recognizer.Canceled += (sender, e) => 
            {
                stopRecognition.TrySetResult(0);
            };
            recognizer.SessionStopped += (sender, e) => 
            {
                stopRecognition.TrySetResult(0);
            };
            //recognizer.Synthesizing+= (sender, e) => 
            //{
            //    var audio = e.Result.GetAudio();
            //    Console.WriteLine($"Audio synthesized: {audio.Length:#,0} byte(s) {(audio.Length == 0 ? "(Complete)" : "")}");

            //    if (audio.Length > 0)
            //    {
            //        File.WriteAllBytes("YourAudioFile.wav", audio);
            //    }
            //};
            await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

            // Waits for completion.
            // Use Task.WaitAny to keep the task rooted.
            Task.WaitAny(new[] { stopRecognition.Task });

            // Stops recognition.
            await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
        }


        /// <summary>
        /// Assess the pronunciation of a spoken dialogue. Uses the default microphone as input
        /// </summary>
        /// <param name="referenceText">A text to compare the with the recognized spoken dialogue</param>
        /// <returns></returns>
        public async Task<PronunciationAssessmentResult> AssessPronunciationFromDefaultMicrophone(string referenceText)
        {
            var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            PronunciationAssessmentConfig pronunciationAssessmentConfig =
                new PronunciationAssessmentConfig(referenceText,
                GradingSystem.HundredMark, Granularity.Phoneme);
            using (var recognizer = new SpeechRecognizer(this.SpeechConfig, audioConfig))
            {
                pronunciationAssessmentConfig.ApplyTo(recognizer);
                var speechRecognitionResult = await recognizer.RecognizeOnceAsync();
                var pronunciationAssessmentResult = PronunciationAssessmentResult.FromResult(speechRecognitionResult);
                return pronunciationAssessmentResult;
            }
        }
    }
}
