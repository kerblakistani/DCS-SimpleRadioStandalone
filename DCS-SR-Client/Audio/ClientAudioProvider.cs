﻿using System;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using NAudio.Dsp;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client
{
    public class ClientAudioProvider : RadioAudioProvider
    {
        public static readonly int SILENCE_PAD = 100;

        private readonly Random _random = new Random();

        private readonly BiQuadFilter _highPassFilter;
        private readonly BiQuadFilter _lowPassFilter;


        public ClientAudioProvider() : base(AudioManager.INPUT_SAMPLE_RATE)
        {
            _highPassFilter = BiQuadFilter.HighPassFilter(AudioManager.INPUT_SAMPLE_RATE, 520, 0.97f);
            _lowPassFilter = BiQuadFilter.LowPassFilter(AudioManager.INPUT_SAMPLE_RATE, 4130, 2.0f);
        }

        public long LastUpdate { get; private set; }

        public void AddClientAudioSamples(ClientAudio audio)
        {
            VolumeSampleProvider.Volume = 1.0f;
            //sort out volume

            var decrytable = audio.Decryptable || (audio.Encryption == 0);

            if (decrytable)
            {
                //adjust for LOS + Distance + Volume
                AdjustVolume(audio);

                var addEffect = _settings.UserSettings[(int) SettingType.RadioEffects] == "ON";

                if (addEffect)
                {
                    AddRadioEffect(audio);
                }
            }
            else
            {
                AddEncryptionFailureEffect(audio);
            }

            long now = Environment.TickCount;
            if (now - LastUpdate > 160)
            {
                //append 100ms of silence - this functions as our jitter buffer??
                var silencePad = AudioManager.INPUT_SAMPLE_RATE/1000*SILENCE_PAD;

                var newAudio = new short[audio.PcmAudioShort.Length + silencePad];

                Buffer.BlockCopy(audio.PcmAudioShort, 0, newAudio, silencePad, audio.PcmAudioShort.Length);

                audio.PcmAudioShort = newAudio;
            }

            LastUpdate = Environment.TickCount;

            AddAudioSamples(ConversionHelpers.ShortArrayToByteArray(audio.PcmAudioShort), audio.ReceivedRadio, false);
        }

        private void AdjustVolume(ClientAudio clientAudio)
        {
            var audio = clientAudio.PcmAudioShort;
            for (var i = 0; i < audio.Length; i++)
            {
                var speaker1Short = (short) (audio[i]*clientAudio.Volume);
                if (clientAudio.RecevingPower < 0)
                {
                    //calculate % loss - not real percent coz is logs
                    var loss = clientAudio.RecevingPower/RadioCalculator.RXSensivity;

                    //add in radio loss
                    //if more than 0.6 loss reduce volume
                    if (loss > 0.6)
                    {
                        speaker1Short = (short) (speaker1Short*(1.0f - loss));
                    }
                }

                //0 is no loss so if more than 0 reduce volume
                if (clientAudio.LineOfSightLoss > 0)
                {
                    speaker1Short = (short) (speaker1Short*(1.0f - clientAudio.LineOfSightLoss));
                }

                audio[i] = speaker1Short;
            }
        }


        private void AddRadioEffect(ClientAudio clientAudio)
        {
            var mixedAudio = clientAudio.PcmAudioShort;

            for (var i = 0; i < mixedAudio.Length; i++)
            {
                var audio = mixedAudio[i]/32768f;

                audio = _highPassFilter.Transform(audio);

                if (float.IsNaN(audio))
                    audio = _lowPassFilter.Transform(mixedAudio[i]);
                else
                    audio = _lowPassFilter.Transform(audio);

                if (!float.IsNaN(audio))
                {
                    // clip
                    if (audio > 1.0f)
                        audio = 1.0f;
                    if (audio < -1.0f)
                        audio = -1.0f;

                    mixedAudio[i] = (short) (audio*32767);
                }
            }
        }

        private void AddEncryptionFailureEffect(ClientAudio clientAudio)
        {
            var mixedAudio = clientAudio.PcmAudioShort;

            for (var i = 0; i < mixedAudio.Length; i++)
            {
                mixedAudio[i] = RandomShort();
            }
        }

        private short RandomShort()
        {
            //random short at max volume at eights
            return (short) _random.Next(-32768/8, 32768/8);
        }
    }
}