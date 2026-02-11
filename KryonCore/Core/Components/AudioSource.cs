using KrayonCore.Audio;
using KrayonCore.Core.Attributes;
using NAudio.Wave;
using OpenTK.Mathematics;
using System;

namespace KrayonCore.Components
{
    public class AudioSource : Component
    {
        [ToStorage, NoSerializeToInspector] public string audioClip = string.Empty;
        private AudioFileReader audioInfo;

        [ToStorage] public bool Loop = false;
        [ToStorage] public float Volume = 1f;
        [ToStorage] public bool PlayOnAwake = true;
        [Range(0.0f, 1.0f), ToStorage] public float SpatialBlend = 1f;
        [ToStorage] public float MinDistance = 1f;
        [ToStorage] public float MaxDistance = 25f;
        [ToStorage] public float RolloffFactor = 1f;
        [ToStorage] public float SmoothingSpeed = 25f;

        private Vector3 lastPosition;
        private float currentPan = 0f;
        private float currentVolumeMult = 1f;
        private float previousAngle = 0f;
        private bool angleInitialized = false;

        public string AudioClip
        {
            get => audioClip;
            set
            {
                if (audioClip == value)
                    return;
                audioClip = value;
                RecreateAudio();
            }
        }

        public override void Awake()
        {
            lastPosition = GameObject.Transform.Position;
            currentPan = 0f;
            currentVolumeMult = 1f;
            angleInitialized = false;

            if (PlayOnAwake)
                RecreateAudio();
        }

        private void RecreateAudio()
        {
            if (string.IsNullOrEmpty(audioClip))
                return;

            AudioEngine.Stop();
            var asset = AssetManager.Get(Guid.Parse(audioClip));
            if (asset == null)
                return;

            audioInfo = AudioEngine.Play(asset.Path, Loop);
            lastPosition = GameObject.Transform.Position;
            currentPan = 0f;
            currentVolumeMult = 1f;
            angleInitialized = false;
            UpdateSpatial(0.016f);
        }

        public override void Update(float DeltaTime)
        {
            UpdateSpatial(DeltaTime);
        }

        private void UpdateSpatial(float deltaTime)
        {
            if (audioInfo == null)
                return;

            var camera = GraphicsEngine.Instance.GetSceneRenderer().GetCamera();

            Vector3 listenerPos = camera.Position;
            Vector3 listenerForward = camera.Front;
            Vector3 listenerRight = camera.Right;
            Vector3 sourcePos = GameObject.Transform.Position;

            Vector3 toSource = sourcePos - listenerPos;
            float distance = toSource.Length;

            if (distance < 0.01f)
            {
                float lerpFactor = Math.Clamp(SmoothingSpeed * deltaTime, 0f, 1f);
                currentPan = MathHelper.Lerp(currentPan, 0f, lerpFactor);
                currentVolumeMult = MathHelper.Lerp(currentVolumeMult, 1f, lerpFactor);

                AudioEngine.SetVolume(Volume);
                AudioEngine.SetPan(0f);
                return;
            }

            float distanceVolume = 1f;
            if (distance > MinDistance)
            {
                if (distance < MaxDistance)
                {
                    float normalizedDistance = (distance - MinDistance) / (MaxDistance - MinDistance);
                    distanceVolume = 1f - MathF.Pow(normalizedDistance, RolloffFactor);
                }
                else
                {
                    distanceVolume = 0f;
                }
            }

            Vector3 forwardFlat = new Vector3(listenerForward.X, 0f, listenerForward.Z);
            Vector3 rightFlat = new Vector3(listenerRight.X, 0f, listenerRight.Z);
            Vector3 toSourceFlat = new Vector3(toSource.X, 0f, toSource.Z);

            if (forwardFlat.LengthSquared < 0.001f || rightFlat.LengthSquared < 0.001f || toSourceFlat.LengthSquared < 0.001f)
            {
                float lerpFactor = Math.Clamp(SmoothingSpeed * deltaTime, 0f, 1f);
                currentPan = MathHelper.Lerp(currentPan, 0f, lerpFactor);
                currentVolumeMult = MathHelper.Lerp(currentVolumeMult, 1f, lerpFactor);

                float finalVol = MathHelper.Lerp(Volume, Volume * distanceVolume * currentVolumeMult, SpatialBlend);
                AudioEngine.SetVolume(Math.Clamp(finalVol, 0f, 1f));
                AudioEngine.SetPan(currentPan * SpatialBlend);
                return;
            }

            forwardFlat = forwardFlat.Normalized();
            rightFlat = rightFlat.Normalized();
            toSourceFlat = toSourceFlat.Normalized();

            float dotRight = Vector3.Dot(toSourceFlat, rightFlat);
            float dotForward = Vector3.Dot(toSourceFlat, forwardFlat);

            float currentAngle = MathF.Atan2(dotRight, dotForward);

            if (!angleInitialized)
            {
                previousAngle = currentAngle;
                angleInitialized = true;
            }

            float angleDelta = currentAngle - previousAngle;

            if (angleDelta > MathF.PI)
                angleDelta -= MathF.PI * 2f;
            else if (angleDelta < -MathF.PI)
                angleDelta += MathF.PI * 2f;

            float smoothedAngle = previousAngle + angleDelta;
            previousAngle = smoothedAngle;

            float targetPan = MathF.Sin(smoothedAngle);
            targetPan = Math.Clamp(targetPan, -1f, 1f);

            float rearFactor = (MathF.Cos(smoothedAngle) + 1f) * 0.5f;
            float targetVolumeMult = MathHelper.Lerp(0.65f, 1f, rearFactor);

            float lerpSpeed = Math.Clamp(SmoothingSpeed * deltaTime, 0f, 1f);
            currentPan = MathHelper.Lerp(currentPan, targetPan, lerpSpeed);
            currentVolumeMult = MathHelper.Lerp(currentVolumeMult, targetVolumeMult, lerpSpeed);

            currentPan = Math.Clamp(currentPan, -1f, 1f);

            float spatialVolume = Volume * distanceVolume * currentVolumeMult;
            float finalVolume = MathHelper.Lerp(Volume, spatialVolume, SpatialBlend);
            finalVolume = Math.Clamp(finalVolume, 0f, 1f);

            float finalPan = currentPan * SpatialBlend;

            AudioEngine.SetVolume(finalVolume);
            AudioEngine.SetPan(finalPan);
        }

        public void Play() => RecreateAudio();
        public void Stop() => AudioEngine.Stop();
        public void Pause() => AudioEngine.Pause();
        public void Resume() => AudioEngine.Resume();
    }
}