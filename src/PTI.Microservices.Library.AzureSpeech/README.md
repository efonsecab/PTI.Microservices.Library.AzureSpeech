# PTI.Microservices.Library.AzureSpeech

This is part of PTI.Microservices.Library set of packages

The purpose of this package is to facilitate the calls to Azure Speech APIs, while maintaining a consistent usage pattern among the different services in the group

**Examples:**

## Translate Continuous Speech

    AzureSpeechService azureSpeechService =
       new AzureSpeechService(null, this.AzureSpeechConfiguration, new CustomHttpClient(new CustomHttpClientHandler(null)));
    await azureSpeechService.TranslateContinuousSpeechAsync("en-US", "es", "es-MX-HildaRUS");

## Assess Pronunciation
    AzureSpeechService azureSpeechService =
       new AzureSpeechService(null, this.AzureSpeechConfiguration, new CustomHttpClient(new CustomHttpClientHandler(null)));
    var result = await azureSpeechService.AssessPronunciationFromDefaultMicrophone("This NuGet package is the best out there!");

## Get Text From Audio Stream
    Stream audioFile = File.Open(@"C:\Temp\TestAudio.wav", FileMode.Open);
    AzureSpeechService azureSpeechService =
       new AzureSpeechService(null, this.AzureSpeechConfiguration, new CustomHttpClient(new CustomHttpClientHandler(null)));
    var result = await azureSpeechService.GetTextFromAudioStreamAsync(audioFile);

## TalkToAsByteArray
    AzureSpeechService azureSpeechService =
       new AzureSpeechService(null, this.AzureSpeechConfiguration, new CustomHttpClient(new CustomHttpClientHandler(null)));
    MemoryStream memoryStream = new MemoryStream();
    var result = await azureSpeechService.TalkToStreamAsync("Hello Pocahontas!",
       outputStream: memoryStream);
    memoryStream.Position = 0;
    File.WriteAllBytes(@"C:\Temp\TestAudio.wav", memoryStream.ToArray());
    memoryStream.Close();

## Speak
    AzureSpeechService azureSpeechService =
       new AzureSpeechService(null, this.AzureSpeechConfiguration, new CustomHttpClient(new CustomHttpClientHandler(null)));
    var result = await azureSpeechService.TalkToDefaultSpeakersAsync("This is an audio generated from tests");

## Create Audio Stream
    AzureSpeechService azureSpeechService =
       new AzureSpeechService(null, this.AzureSpeechConfiguration, new CustomHttpClient(new CustomHttpClientHandler(null)));
    var (result, outputStream) = await azureSpeechService.CreateAudioStreamAsync("This is an audio generated from tests");

## SSML
    AzureSpeechService azureSpeechService =
       new AzureSpeechService(null, this.AzureSpeechConfiguration, new CustomHttpClient(new CustomHttpClientHandler(null)));
    SSMLMessage message = new SSMLMessage()
    {
        lang = "en-US",
        version = 1.0M,
        voice = new speakVoice()
        {
            expressas = new expressas()
            {
                style = "cheerful",
                Value = "This is a test voice using the SSML language"
            },
            name = "Microsoft Server Speech Text to Speech Voice (en-US, AriaNeural)",
            prosody = new speakVoiceProsody[]
            {
                new speakVoiceProsody()
                {
                    pitch="-50.00%",
                    Value="This is an example of pitch being changed in a whole paragraph."
                },
                new speakVoiceProsody()
                {
                    rate="-60.00%",
                    Value="This is an example of rate being changed in a whole paragraph."
                },
                new speakVoiceProsody()
                {
                    volume="-50.00%",
                    Value="This is an example of volume being changed in a whole paragraph."
                },
                new speakVoiceProsody()
                {
                    volume="loud",
                    Value="This is an example of volume being changed in a whole paragraph, but using Constant"
                }
            }
        }
    };
    var result = await azureSpeechService.TalkToDefaultSpeakersWithSSML(message);