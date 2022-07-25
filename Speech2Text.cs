using Google.Apis.Auth.OAuth2;
using Google.Cloud.Speech.V1;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Google.Cloud.Speech.V1.SpeechClient;

namespace SpeechToTextGoogle
{
    public class Speech2Text
    {
        private WaveFormat recordingFormat;
        private WaveBuffer encoderInputBuffer;
        private WaveInEvent waveInStream;
        private SpeechClient speechClient;
        private StreamingRecognizeStream streamingCall;
        private RecognitionConfig recognitionConfig;
        private DateTime startTime;
        private DateTime realStartTime;
        private CancellationTokenSource tokenSource = new CancellationTokenSource();
    
        public Speech2Text(int sampleRate, string authPath)
        {
            this.recordingFormat = new WaveFormat(sampleRate, 16, 1);

            waveInStream = new WaveInEvent();
            waveInStream.DeviceNumber = 0;

            waveInStream.BufferMilliseconds = 100;
            waveInStream.WaveFormat = this.recordingFormat;
            waveInStream.DataAvailable += new EventHandler<WaveInEventArgs>(OnDataAvailable);
            recognitionConfig = new RecognitionConfig()
            {
                Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                SampleRateHertz = waveInStream.WaveFormat.SampleRate,
                EnableAutomaticPunctuation = true,
                Metadata = new RecognitionMetadata()
                {
                    InteractionType = RecognitionMetadata.Types.InteractionType.Discussion,
                    MicrophoneDistance = RecognitionMetadata.Types.MicrophoneDistance.Nearfield,
                    RecordingDeviceType = RecognitionMetadata.Types.RecordingDeviceType.Pc,
                    OriginalMediaType = RecognitionMetadata.Types.OriginalMediaType.Audio
                },
                AudioChannelCount = 1,
                LanguageCode = "en"
            };
            var builder = new SpeechClientBuilder();
            builder.CredentialsPath = authPath;
            speechClient = builder.Build();
            streamingCall = speechClient.StreamingRecognize();
        }
        private async Task ReadResponses()
        {
            await using var responseStream = streamingCall.GetResponseStream();

            while (await responseStream.MoveNextAsync())
            {
                foreach (var result in responseStream.Current.Results)
                    Console.WriteLine(result);

                if (tokenSource.Token.IsCancellationRequested)
                    tokenSource.Token.ThrowIfCancellationRequested();
            }
        }

        public async Task Start()
        {
            streamingCall = speechClient.StreamingRecognize();

            await streamingCall.WriteAsync(
                new StreamingRecognizeRequest()
                {
                    StreamingConfig = new StreamingRecognitionConfig()
                    {
                        Config = recognitionConfig,
                        InterimResults = false,
                        SingleUtterance = false
                    }
                });

            startTime = DateTime.Now;
            realStartTime = startTime;
            waveInStream.StartRecording();

            await Task.Run(async () => await ReadResponses(), tokenSource.Token);
        }
        public void Stop()
        {
            waveInStream.StopRecording();
            tokenSource.Cancel();
        }
        private async void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            double estimatedTime = (DateTime.Now - startTime).TotalMinutes;

            if (estimatedTime >= 5)
            {
                Console.WriteLine("Rested connection!");

                tokenSource.Cancel();

                await streamingCall.WriteCompleteAsync();
                streamingCall = speechClient.StreamingRecognize();

                await Task.Run(async() => await ReadResponses(), tokenSource.Token);

                await streamingCall.WriteAsync(
                    new StreamingRecognizeRequest()
                    {
                        StreamingConfig = new StreamingRecognitionConfig()
                        {
                            Config = recognitionConfig,
                            InterimResults = false,
                            SingleUtterance = false
                        }
                    });
                await streamingCall.WriteAsync(
              new StreamingRecognizeRequest()
              {
                  AudioContent = Google.Protobuf.ByteString.CopyFrom(e.Buffer, 0, e.Buffer.Length),
              });

                startTime = DateTime.Now;
            }
            else
            {
                await streamingCall.WriteAsync(
                  new StreamingRecognizeRequest()
                  {
                      AudioContent = Google.Protobuf.ByteString.CopyFrom(e.Buffer, 0, e.Buffer.Length),
                  });
            }
        }
    }
}