using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public sealed class MenuAtmosphere : MonoBehaviour
    {
        public ParticleSystem dust;
        public MenuIsland[] islands;

        public const int Capacity = 24;
        private readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[Capacity];
        private readonly Vector3[] positions = new Vector3[Capacity];

        private void Awake()
        {
            var random = new System.Random(1103);
            for (int i = 0; i < Capacity; i++)
                positions[i] = new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
        }

        internal void Present(float assembly, MainMenu.View view, int selected, bool reducedMotion)
        {
            int quality = (int)CampaignSettings.GraphicsQuality;
            int count = quality == 0 ? 8 : quality == 1 ? 16 : Capacity;
            if (view != MainMenu.View.Intro)
                count = 0;
            float time = reducedMotion ? 0f : Time.unscaledTime;
            bool focus = view is MainMenu.View.General or MainMenu.View.Audio or MainMenu.View.Entering;
            for (int i = 0; i < count; i++)
            {
                Vector3 seed = positions[i];
                int islandIndex = focus && selected >= 0 ? selected : i % islands.Length;
                MenuIsland island = islands[islandIndex];
                float motion = island.Restored;
                float pulse = Mathf.Sin(motion * Mathf.PI);
                float phi = seed.x * Mathf.PI * 2f + time * (.09f + seed.y * .12f) - motion * .9f;
                float radius = 2f + seed.y * (2.9f + (1f - assembly) * 2f) - pulse * .8f;
                var point = island.transform.position + new Vector3(Mathf.Cos(phi) * radius,
                    (seed.z - .35f) * (5.7f + (1f - assembly) * 3f) + Mathf.Sin(time * .8f + seed.x * 9f) * .21f,
                    Mathf.Sin(phi) * radius);
                float size = (.018f + .020f * seed.x) * (.84f + .16f * Mathf.Sin(time * .62f + seed.x * 31f));
                particles[i].position = point;
                particles[i].startSize = size * (1f + pulse * .5f);
                particles[i].startColor = new Color32(237, 213, 168, 255);
                particles[i].remainingLifetime = particles[i].startLifetime = 10f;
                particles[i].rotation3D = new Vector3(seed.x, seed.z, seed.y) * 360f;
            }
            dust.SetParticles(particles, count);
        }
    }
}
