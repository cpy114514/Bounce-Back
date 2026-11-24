using System.Collections;
using BounceBack.GeometryWorld;
using UnityEngine;

namespace BounceBack.ArtIntegration
{
    /// <summary>Fallback ripples used only when the stylized pool mesh is absent.</summary>
    [DisallowMultipleComponent]
    public sealed class SimplePoolRings : MonoBehaviour
    {
        ElasticGeometry pool;
        Material lineMaterial;

        void OnEnable()
        {
            pool = GetComponent<ElasticGeometry>();
            if (pool)
                pool.BallCompressed += Burst;
        }

        void OnDisable()
        {
            if (pool)
                pool.BallCompressed -= Burst;
        }

        void Burst(ElasticGeometry elastic, BallBody ball) => StartCoroutine(Expand(ball.Body.position));

        IEnumerator Expand(Vector3 point)
        {
            var line = new GameObject("Pool Ring").AddComponent<LineRenderer>();
            line.transform.SetParent(transform, true);
            line.loop = true;
            line.positionCount = 40;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.useWorldSpace = true;
            if (!lineMaterial)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                lineMaterial = shader ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
                lineMaterial.color = new Color(0.82f, 0.95f, 0.95f, 1f);
            }
            line.sharedMaterial = lineMaterial;
            const float duration = 0.7f;
            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                float radius = Mathf.Lerp(0.18f, 1.65f, time / duration);
                line.widthMultiplier = Mathf.Lerp(0.045f, 0.01f, time / duration);
                for (int i = 0; i < line.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / line.positionCount;
                    line.SetPosition(i, point + new Vector3(Mathf.Cos(angle) * radius, 0.03f, Mathf.Sin(angle) * radius));
                }
                yield return null;
            }
            Destroy(line.gameObject);
        }
    }
}
