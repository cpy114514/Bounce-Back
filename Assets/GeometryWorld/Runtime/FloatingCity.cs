using UnityEngine;
using UnityEngine.InputSystem;

namespace BounceBack.GeometryWorld
{
    public sealed class FloatingCity : MonoBehaviour
    {
        public Camera view;
        public Transform ball;
        public Transform ballVisual;
        public Transform[] waypoints;
        public WorldEntrance[] entrances;
        public int perspectiveSpan = -1;
        [Min(0.1f)] public float speed = 3.8f;
        [Min(0.1f)] public float interactionRange = 1.2f;
        public GeometryMusicLoop ambienceMotion;
        public GUISkin hudSkin;
        public GameObject finaleGarden;
        public BoxCollider gardenFloor;
        public GeometryPose finaleTransform;
        public GeometryPose gardenInteraction;
        public AudioSource finaleSound;

        private WorldRoute route;
        private CampaignProgress progress;
        private float along, target, portalTarget = -1f, finaleTime, artPulse;
        private Vector3 cameraOffset, cameraVelocity, gardenTarget;
        private bool reducedMotion, arrows, audioReady, loading, exploringGarden, showNotice;
        private WorldEntrance closest;
        private static readonly Rect MenuButton = new(14f, 14f, 110f, 29f);

        private void Start()
        {
            if (waypoints.Length < 2 || !ball || !view)
            {
                enabled = false;
                return;
            }
            route = new WorldRoute(waypoints);
            progress = CampaignProgress.Load();
            CampaignSettings.Apply();
            reducedMotion = CampaignSettings.ReducedMotion;
            if (ambienceMotion)
            {
                ambienceMotion.ReduceMotion = reducedMotion;
                ambienceMotion.UseDefaultCityTheme();
            }
            arrows = CampaignSettings.ArrowKeys;
            cameraOffset = view.transform.position - waypoints[0].position;
            int arrival = Mathf.Clamp(CampaignScenes.WorldEntrance, 0, Mathf.Max(0, entrances.Length - 1));
            along = target = entrances.Length > 0 ? route.AtWorldPosition(entrances[arrival].transform.position) : 0f;
            ball.position = route.At(along);
            view.transform.position = ball.position + cameraOffset;

            foreach (var entrance in entrances)
                entrance.ShowProgress(progress, reducedMotion || !CampaignScenes.PlayUnlockReveal);
            if (finaleGarden)
                finaleGarden.SetActive(progress.IsFinished);
            if (finaleTransform && progress.FinaleSeen)
                finaleTransform.Apply(1f);
        }

        private void Update()
        {
            if (loading)
                return;
            Keyboard keyboard = Keyboard.current;
            Pointer pointer = Pointer.current;
            if (pointer?.press.wasPressedThisFrame == true || keyboard?.anyKey.wasPressedThisFrame == true)
            {
                audioReady = true;
                if (ambienceMotion)
                    ambienceMotion.StartAudio(progress.CompletedCount + 1);
            }
            if (progress.IsFinished && !progress.FinaleSeen)
            {
                PlayFinale();
                return;
            }

            bool interact = keyboard?.eKey.wasPressedThisFrame == true || keyboard?.enterKey.wasPressedThisFrame == true;
            Vector2 move = CampaignSettings.ReadMove(keyboard, arrows);
            Vector3 direction = (Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized * move.y +
                Vector3.ProjectOnPlane(view.transform.right, Vector3.up).normalized * move.x).normalized;
            bool clicked = pointer?.press.wasPressedThisFrame == true;
            Vector2 cursor = pointer?.position.ReadValue() ?? Vector2.zero;
            clicked &= !MenuButton.Contains(new Vector2(cursor.x, Screen.height - cursor.y) / GeometryLevel.HudScale);

            var previous = ball.position;
            if (exploringGarden)
                MoveInGarden(direction, clicked, cursor);
            else
                MoveOnRoute(direction, clicked, cursor);
            Follow(previous);
            closest = null;
            float best = float.PositiveInfinity;
            foreach (var entrance in entrances)
            {
                float distance = (entrance.transform.position - ball.position).sqrMagnitude;
                if (distance >= best)
                    continue;
                closest = entrance;
                best = distance;
            }
            if (closest)
            {
                RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, closest.ambientLight, Time.deltaTime * 1.5f);
                foreach (var entrance in entrances)
                    entrance.UpdateAmbience(entrance == closest, audioReady);
            }
            if (best > interactionRange * interactionRange)
                closest = null;
            if (interact && closest)
                loading = CampaignScenes.Enter(closest.levelIndex);
            else if (interact && exploringGarden)
                artPulse = 1f;
            if (gardenInteraction && artPulse > 0f)
            {
                artPulse = Mathf.MoveTowards(artPulse, 0f, Time.deltaTime / 1.3f);
                gardenInteraction.Apply(Mathf.Sin(Mathf.PI * artPulse));
            }
        }

        private void MoveOnRoute(Vector3 direction, bool clicked, Vector2 cursor)
        {
            bool keyboard = direction.sqrMagnitude > 0f;
            if (clicked)
            {
                if (CanExploreGarden() && TryPickGarden(cursor, out Vector3 point))
                {
                    gardenTarget = point;
                    target = route.Length;
                }
                else
                    target = route.Pick(view, cursor, 51f * GeometryLevel.HudScale, along, perspectiveSpan);
            }
            float goal = keyboard ? route.KeyboardTarget(along, direction) : target;
            if (portalTarget >= 0f)
                goal = portalTarget;
            bool forward = goal > along;
            int currentSpan = route.Segment(along, forward);
            float step = speed * Time.deltaTime;
            if (currentSpan == perspectiveSpan && Mathf.Abs(goal - along) > 0.0001f)
            {
                portalTarget = route.distance[forward ? currentSpan + 1 : currentSpan];
                float span = route.distance[currentSpan + 1] - route.distance[currentSpan];
                goal = portalTarget;
                step = Mathf.Max(step, span * Time.deltaTime / 0.26f);
            }
            along = Mathf.MoveTowards(along, goal, step);
            if (portalTarget >= 0f && Mathf.Abs(along - portalTarget) < 0.001f)
            {
                portalTarget = -1f;
                if (keyboard)
                    target = along;
            }
            else if (keyboard)
                target = along;
            ball.position = route.At(along);
            if (along >= route.Length && CanExploreGarden() &&
                (gardenTarget != Vector3.zero || Vector3.Dot(direction, Vector3.forward) > 0.25f))
            {
                exploringGarden = true;
                if (gardenTarget == Vector3.zero)
                    gardenTarget = ball.position;
            }
        }

        private bool CanExploreGarden() => progress.FinaleSeen && gardenFloor;

        private bool TryPickGarden(Vector2 cursor, out Vector3 point)
        {
            var ray = view.ScreenPointToRay(cursor);
            if (gardenFloor.Raycast(ray, out var hit, view.farClipPlane))
            {
                point = new Vector3(hit.point.x, route.points[^1].y, hit.point.z);
                return true;
            }
            point = Vector3.zero;
            return false;
        }

        private void MoveInGarden(Vector3 direction, bool clicked, Vector2 cursor)
        {
            Vector3 gate = route.points[^1];
            if (clicked)
                gardenTarget = TryPickGarden(cursor, out Vector3 picked) ? picked : gate;
            if (direction.sqrMagnitude > 0f)
                gardenTarget = ball.position + direction * speed * Time.deltaTime;
            Vector3 point = Vector3.MoveTowards(ball.position, gardenTarget, speed * Time.deltaTime);
            Bounds bounds = gardenFloor.bounds;
            point.x = Mathf.Clamp(point.x, bounds.min.x + 0.35f, bounds.max.x - 0.35f);
            point.z = Mathf.Clamp(point.z, gate.z, bounds.max.z - 0.35f);
            ball.position = point;
            if (point.z <= gate.z + 0.001f && Mathf.Abs(point.x - gate.x) < 0.35f &&
                (direction.z < -0.1f || gardenTarget == gate && direction == Vector3.zero))
            {
                exploringGarden = false;
                gardenTarget = Vector3.zero;
                target = clicked ? route.Pick(view, cursor, 51f * GeometryLevel.HudScale, along, perspectiveSpan) : along;
            }
        }

        private void Follow(Vector3 previous)
        {
            Vector3 displacement = ball.position - previous;
            if (ballVisual && displacement.sqrMagnitude > 0.000001f && portalTarget < 0f)
                ballVisual.Rotate(Vector3.Cross(Vector3.up, displacement.normalized), displacement.magnitude * 150f, Space.World);
            view.transform.position = Vector3.SmoothDamp(view.transform.position, ball.position + cameraOffset,
                ref cameraVelocity, reducedMotion ? 0.08f : 0.27f);
        }

        private void PlayFinale()
        {
            if (finaleTime == 0f && finaleSound && finaleSound.clip)
                finaleSound.Play();
            finaleTime += Time.unscaledDeltaTime;
            float time = Mathf.Clamp01(finaleTime / (reducedMotion ? 0.3f : 4f));
            if (finaleTransform)
                finaleTransform.Apply(Mathf.SmoothStep(0f, 1f, time));
            if (time >= 1f)
            {
                progress.MarkFinaleSeen();
                progress.Save();
                showNotice = true;
            }
        }

        private void OnGUI()
        {
            if (progress == null || loading)
                return;
            var original = GUI.matrix;
            var originalSkin = GUI.skin;
            if (hudSkin)
                GUI.skin = hudSkin;
            float scale = GeometryLevel.HudScale;
            GUI.matrix = original * Matrix4x4.Scale(Vector3.one * scale);
            try
            {
                if (GUI.Button(MenuButton, "Main Menu"))
                    loading = CampaignScenes.OpenMenu();
                string message;
                if (closest)
                {
                    int index = closest.levelIndex;
                    string state = progress.IsUnlocked(index) ? "E / Enter to play" : $"Complete Level {index} to unlock";
                    message = $"{closest.districtName}  ·  {state}";
                }
                else if (exploringGarden)
                    message = "E / Enter to move the garden sculpture";
                else if (showNotice)
                    message = "Floating garden open beyond the fourth district";
                else
                    message = arrows ? "Arrow keys or click the path" : "WASD or click the path";
                GUI.Box(new Rect(14f, 53f, 310f, 28f), message);
            }
            finally
            {
                GUI.matrix = original;
                GUI.skin = originalSkin;
            }
        }
    }
}
