using System;
using System.Collections.Generic;
using Glasscore.Net;
using UnityEngine;

namespace Glasscore.Client
{
    /// <summary>
    /// Proximity VOIP over the game's own UDP channel: 16 kHz mono, 20 ms frames, G.711 μ-law
    /// (128 kbit/s — fine on LAN/broadband, no codec dependency). Push-to-talk (V) or open mic with a
    /// simple voice-activity gate; D-Pad Down / M toggles mute. In a match each speaker's voice is a
    /// 3D AudioSource on their avatar, in the lobby it is 2D.
    /// </summary>
    public sealed class VoiceChat : IDisposable
    {
        public const int SampleRate = 16000;
        public const int FrameSamples = 320;
        private const float VadThreshold = 0.012f;
        private const float VadHangSeconds = 0.4f;

        private sealed class RemoteStream
        {
            public GameObject Go;
            public AudioSource Source;
            public readonly Queue<float> Buffer = new Queue<float>();
            public readonly object Lock = new object();
            public float LastHeard;
        }

        private readonly Dictionary<byte, RemoteStream> _remote = new Dictionary<byte, RemoteStream>();
        private readonly float[] _frame = new float[FrameSamples];
        private readonly byte[] _encoded = new byte[FrameSamples];
        private readonly Transform _parent;
        private AudioClip _mic;
        private string _device;
        private int _readPos;
        private int _frameFill;
        private float _vadHang;
        private float[] _scratch = new float[SampleRate];

        public bool Muted;
        public bool Transmitting { get; private set; }
        public string MicError { get; private set; }

        public VoiceChat(Transform parent) { _parent = parent; }

        public static string[] InputDevices()
        {
            var list = new List<string> { GameSettings.SystemDefaultDevice };
            list.AddRange(Microphone.devices);
            return list.ToArray();
        }

        public bool IsTalking(byte slot) => _remote.TryGetValue(slot, out var s) && Time.time - s.LastHeard < 0.3f;

        public void Update(GameClient client, GameSettings settings, InputService input, Func<byte, Vector3?> speakerPosition)
        {
            if (input.MuteTogglePressed)
            {
                Muted = !Muted;
                AudioService.Instance?.Play(AudioService.TinkHigh, 0.5f, Muted ? 0.7f : 1.3f);
            }

            bool connected = client != null && client.Status != ClientStatus.Disconnected && client.Status != ClientStatus.Connecting;
            if (!settings.VoiceEnabled || !connected)
            {
                StopMic();
                Transmitting = false;
            }
            else
            {
                EnsureMic(settings.VoiceInputDevice);
                PumpMic(client, settings, input);
            }

            foreach (var kv in _remote)
            {
                RemoteStream r = kv.Value;
                r.Source.volume = settings.VoiceGain;
                Vector3? pos = speakerPosition(kv.Key);
                r.Source.spatialBlend = pos.HasValue ? 1f : 0f;
                if (pos.HasValue) r.Go.transform.position = pos.Value + Vector3.up * 1.6f;
            }
        }

        private void EnsureMic(string device)
        {
            string wanted = device == GameSettings.SystemDefaultDevice ? null : device;
            if (_mic != null && wanted == _device) return;
            StopMic();
            if (Microphone.devices.Length == 0)
            {
                MicError = "No microphone found";
                return;
            }
            _device = wanted;
            _mic = Microphone.Start(_device, true, 1, SampleRate);
            _readPos = 0;
            MicError = _mic == null ? "Microphone unavailable" : null;
        }

        private void StopMic()
        {
            if (_mic == null) return;
            Microphone.End(_device);
            _mic = null;
        }

        private void PumpMic(GameClient client, GameSettings settings, InputService input)
        {
            if (_mic == null) return;
            int pos = Microphone.GetPosition(_device);
            if (pos < 0 || pos == _readPos) return;
            int available = pos > _readPos ? pos - _readPos : _mic.samples - _readPos + pos;
            if (_scratch.Length < available) _scratch = new float[available];

            // Read with wrap-around.
            int first = Math.Min(available, _mic.samples - _readPos);
            var a = new float[first];
            _mic.GetData(a, _readPos);
            Array.Copy(a, 0, _scratch, 0, first);
            if (available > first)
            {
                var b = new float[available - first];
                _mic.GetData(b, 0);
                Array.Copy(b, 0, _scratch, first, b.Length);
            }
            _readPos = pos;

            bool wantsToTalk = !Muted && (settings.PushToTalk ? input.PushToTalkHeld : true);
            for (int i = 0; i < available; i++)
            {
                _frame[_frameFill++] = _scratch[i];
                if (_frameFill < FrameSamples) continue;
                _frameFill = 0;

                float rms = 0f;
                for (int k = 0; k < FrameSamples; k++) rms += _frame[k] * _frame[k];
                rms = Mathf.Sqrt(rms / FrameSamples);
                if (rms > VadThreshold) _vadHang = VadHangSeconds;
                else _vadHang -= FrameSamples / (float)SampleRate;

                bool send = wantsToTalk && (settings.PushToTalk || _vadHang > 0f);
                Transmitting = send;
                if (!send) continue;
                for (int k = 0; k < FrameSamples; k++) _encoded[k] = MuLaw.Encode(_frame[k]);
                client.SendVoice(_encoded, FrameSamples);
            }
        }

        public void Receive(byte slot, ushort seq, byte[] data)
        {
            if (!_remote.TryGetValue(slot, out var r)) r = CreateStream(slot);
            r.LastHeard = Time.time;
            lock (r.Lock)
            {
                // Bound latency: drop audio older than ~300 ms if the buffer ever backs up.
                while (r.Buffer.Count > SampleRate * 3 / 10) r.Buffer.Dequeue();
                foreach (byte b in data) r.Buffer.Enqueue(MuLaw.Decode(b));
            }
        }

        private RemoteStream CreateStream(byte slot)
        {
            var r = new RemoteStream { Go = new GameObject("Voice_" + slot) };
            r.Go.transform.SetParent(_parent, false);
            r.Source = r.Go.AddComponent<AudioSource>();
            r.Source.rolloffMode = AudioRolloffMode.Linear;
            r.Source.minDistance = 4f;
            r.Source.maxDistance = 45f;
            r.Source.loop = true;
            r.Source.clip = AudioClip.Create("VoiceStream_" + slot, SampleRate * 2, 1, SampleRate, true, samples =>
            {
                lock (r.Lock)
                {
                    for (int i = 0; i < samples.Length; i++) samples[i] = r.Buffer.Count > 0 ? r.Buffer.Dequeue() : 0f;
                }
            });
            r.Source.Play();
            _remote[slot] = r;
            return r;
        }

        public void Dispose()
        {
            StopMic();
            foreach (var r in _remote.Values) if (r.Go != null) UnityEngine.Object.Destroy(r.Go);
            _remote.Clear();
        }
    }

    /// <summary>ITU-T G.711 μ-law companding.</summary>
    public static class MuLaw
    {
        private const int Bias = 0x84;
        private const int Clip = 32635;

        public static byte Encode(float sample)
        {
            int pcm = (int)(Mathf.Clamp(sample, -1f, 1f) * 32767f);
            int sign = (pcm >> 8) & 0x80;
            if (sign != 0) pcm = -pcm;
            if (pcm > Clip) pcm = Clip;
            pcm += Bias;
            int exponent = 7;
            for (int mask = 0x4000; (pcm & mask) == 0 && exponent > 0; mask >>= 1) exponent--;
            int mantissa = (pcm >> (exponent + 3)) & 0x0F;
            return (byte)~(sign | (exponent << 4) | mantissa);
        }

        public static float Decode(byte value)
        {
            int u = ~value & 0xFF;
            int sign = u & 0x80;
            int exponent = (u >> 4) & 0x07;
            int mantissa = u & 0x0F;
            int pcm = ((mantissa << 3) + Bias) << exponent;
            pcm -= Bias;
            return (sign != 0 ? -pcm : pcm) / 32768f;
        }
    }
}
