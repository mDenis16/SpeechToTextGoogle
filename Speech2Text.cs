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

        public Speech2Text(int sampleRate)
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
                    InteractionType = RecognitionMetadata.Types.InteractionType.Dictation,
                      MicrophoneDistance = RecognitionMetadata.Types.MicrophoneDistance.Nearfield,
                      RecordingDeviceType = RecognitionMetadata.Types.RecordingDeviceType.Pc,
                      OriginalMediaType = RecognitionMetadata.Types.OriginalMediaType.Audio
                },
                AudioChannelCount = 1,
                LanguageCode = "en",
            };

            speechClient = SpeechClient.Create();
            streamingCall = speechClient.StreamingRecognize();
        }
        private async Task ReadResponses()
        {
            await using var responseStream = streamingCall.GetResponseStream();

            while (await responseStream.MoveNextAsync())
            {
                foreach (var result in responseStream.Current.Results)
                {
                    Console.WriteLine(result);
                }
                if (responseStream.Current.SpeechEventType == StreamingRecognizeResponse.Types.SpeechEventType.EndOfSingleUtterance)
                {
                    streamingCall = speechClient.StreamingRecognize();
                }
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

            waveInStream.StartRecording();
            _ = ReadResponses();

        }
        public void Stop()
        {
            waveInStream.StopRecording();
        }

        private async void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            await streamingCall.WriteAsync(
              new StreamingRecognizeRequest()
              {
                  AudioContent = Google.Protobuf.ByteString.CopyFrom(e.Buffer, 0, e.Buffer.Length),
              });
        }

    }
}