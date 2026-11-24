using System.Collections;
using BounceBack.GeometryWorld;
using BounceBack.StylizedWater;
using UnityEngine;

namespace BounceBack.ArtIntegration
{
    /// <summary>Feeds the delivered pool shader from the existing elastic pool events.</summary>
    [DisallowMultipleComponent]
    public sealed class PoolRippleLink : MonoBehaviour
    {
        ElasticGeometry pool;
        Coroutine routine;

        void OnEnable()
        {
            pool = GetComponent<ElasticGeometry>();
            if (!pool)
                return;
            pool.BallCompressed += Compressed;
            pool.BallRebounded += Rebounded;
        }

        void OnDisable()
        {
            if (!pool)
                return;
            pool.BallCompressed -= Compressed;
            pool.BallRebounded -= Rebounded;
        }

        void Compressed(ElasticGeometry elastic, BallBody ball)
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = StartCoroutine(Rise(Mathf.Max(0.05f, elastic.chargeTime), ball.Body.position));
        }

        void Rebounded(ElasticGeometry elastic, BallBody ball) => FadeFrom(ball.Body.position);

        IEnumerator Rise(float duration, Vector3 point)
        {
            var ripple = FindFirstObjectByType<WaterRipple>();
            if (!ripple)
                yield break;
            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                ripple.SetCharge(time / duration, point);
                yield return null;
            }
            ripple.SetCharge(1f, point);
        }

        void FadeFrom(Vector3 point)
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = StartCoroutine(Fade(point));
        }

        IEnumerator Fade(Vector3 point)
        {
            var ripple = FindFirstObjectByType<WaterRipple>();
            if (!ripple)
                yield break;
            const float duration = 0.45f;
            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                ripple.SetCharge(1f - time / duration, point);
                yield return null;
            }
            ripple.Clear();
        }
    }
}
