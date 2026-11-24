using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BounceBack.GeometryWorld
{
    public sealed class CityRail : MonoBehaviour
    {
        public Transform[] waypoints = Array.Empty<Transform>();
        public DistrictMaterialization[] districts = Array.Empty<DistrictMaterialization>();
        public Vector3 cameraOffset = new(15f, 15f, -15f);
        [Min(1f)] public float cameraSize = 7f;
        [Min(0.1f)] public float maximumSpeed = 5.6f;
        [Min(0.1f)] public float acceleration = 6f;
        [Min(0.1f)] public float approachDistance = 3.2f;
        public GeometryMusicLoop music;
        public GeometryPose roadAssembly;
        public GameObject finaleVisual;
        [Min(-1)] public int crossingSegment = -1;
        [Min(0.02f)] public float crossingDuration = 0.15f;
        public bool firstDistrictOnly;
        public bool requireUnlockedDestination;
        public bool persistNavigationHint;
        internal bool Crossing { get; private set; }

        public float Velocity { get; private set; }
        public int Selected { get; private set; }
        public int Destination { get; private set; }
        public bool AtEntrance => !Crossing && stations != null && Mathf.Abs(Velocity) < 0.001f &&
            Mathf.Abs(along - stations[Destination]) < 0.002f;
        public bool Active { get; private set; }
        public event Action<int> EnterRequested;
        public event Action<int> RoadBlockPlaced;
        public event Action<int> Arrived;

        private float along, hintAlpha, hintAge, finaleAge;
        private bool hintDismissed, audioReady;
        private int roadSignals;
        private float crossingStarted, crossingFrom, crossingTo;
        private int crossingDirection;
        private GameObject[] namePlaques;
        private WorldRoute route;
        private float[] stations;
        private BallBody ball;
        private CampaignProgress progress;
        private PuzzleCameraRig rig;
        private AdaptivePuzzleAudio audio;

        internal void Setup(CampaignProgress save, BallBody player, PuzzleCameraRig cameraRig, AdaptivePuzzleAudio audioDirector)
        {
            progress = save;
            ball = player;
            rig = cameraRig;
            audio = audioDirector;
            if (music)
                music.UseDefaultCityTheme();
            route = new WorldRoute(waypoints);
            stations = new float[districts.Length];
            namePlaques = new GameObject[districts.Length];
            for (int i = 0; i < districts.Length; i++)
            {
                Transform dock = districts[i].carrierAnchor ? districts[i].carrierAnchor : districts[i].transform;
                stations[i] = route.AtWorldPosition(dock.position);
                namePlaques[i] = PreparePlaque(districts[i].entrance, dock.position);
                districts[i].LayerFormed += OnDistrictLayer;
                districts[i].Finished += OnDistrictFormed;
            }
        }

        private void OnDestroy()
        {
            foreach (var district in districts)
            {
                if (!district)
                    continue;
                district.LayerFormed -= OnDistrictLayer;
                district.Finished -= OnDistrictFormed;
            }
        }

        internal void RefreshProgress()
        {
            foreach (var district in districts)
                district.Show(progress, firstDistrictOnly && district.entrance.levelIndex != 0);
            if (finaleVisual)
                finaleVisual.SetActive(progress.IsFinished);
        }

        internal Vector3 Dock(int index) => route.At(stations[index]);

        internal void Open(int index, bool showHint, bool snapCamera)
        {
            along = stations[index];
            Selected = Destination = index;
            Velocity = 0f;
            Crossing = false;
            Active = true;
            ShowPlaques(true);
            if (audio)
                audio.SetMusicSuppressed(true);
            hintAlpha = showHint && (!persistNavigationHint || !progress.TutorialSeen) ? 1f : 0f;
            hintDismissed = false;
            hintAge = 0f;
            finaleAge = 0f;
            SaveArrival(index);
            Roll(ball, Dock(index));
            Frame(Dock(index), snapCamera);
            if (audioReady && music)
                music.StartAudio(progress.CompletedCount + 1);
        }

        internal void Suspend()
        {
            Active = false;
            ShowPlaques(false);
        }

        public bool Step(int direction) => Step(direction, true);

        internal bool Step(int direction, bool playerInput)
        {
            if (!Active || direction == 0 || playerInput && CampaignSceneParticles.IsRunning)
                return false;
            int requested = Mathf.Clamp(Destination + Math.Sign(direction), 0, districts.Length - 1);
            if (requested == Destination || requireUnlockedDestination && !CanEnter(requested))
                return false;
            Destination = requested;
            hintDismissed |= playerInput;
            if (playerInput && persistNavigationHint && hintAlpha > 0f && !progress.TutorialSeen)
            {
                progress.MarkTutorialSeen();
                progress.Save();
            }
            return true;
        }

        public bool Enter()
        {
            if (!Active || CampaignSceneParticles.IsRunning || !AtEntrance || !CanEnter(Selected) ||
                requireUnlockedDestination && !progress.IsMaterialized(Selected))
                return false;
            districts[Selected].Finish();
            Suspend();
            EnterRequested?.Invoke(Selected);
            return true;
        }

        private bool CanEnter(int index) => (!firstDistrictOnly || index == 0) && progress.IsUnlocked(index);

        private GameObject PreparePlaque(WorldEntrance entrance, Vector3 dock)
        {
            var plaque = entrance.transform.Find("Name Plaque") ?? entrance.transform.Find("Architectural Name Plaque");
            if (!plaque)
                return null;
            var carvedName = plaque.Find("Carved Name") ?? entrance.transform.Find("Carved Name");
            if (!carvedName || !carvedName.TryGetComponent(out TextMesh label))
                return null;

            var face = plaque.Find("Plaque Face");
            if (!face)
                face = plaque;
            Quaternion facing = Quaternion.LookRotation(-cameraOffset);
            Vector3 center = dock + facing * new Vector3(-1.30f, -0.82f, -3.25f);
            plaque.SetPositionAndRotation(center, facing);
            face.localScale = new Vector3(2.72f, 0.48f, 0.09f);
            if (face.TryGetComponent(out Renderer backing))
            {
                var tint = new MaterialPropertyBlock();
                tint.SetColor("_BaseColor", new Color(0.13f, 0.28f, 0.42f));
                backing.SetPropertyBlock(tint);
                backing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                backing.receiveShadows = false;
            }

            carvedName.SetParent(entrance.transform, true);
            carvedName.localScale = Vector3.one;
            carvedName.SetPositionAndRotation(center - facing * Vector3.forward * 0.058f, facing);
            label.text = entrance.districtName;
            label.anchor = TextAnchor.MiddleCenter;
            label.characterSize = 0.053f;
            label.fontSize = 72;
            label.color = new Color(1f, 0.96f, 0.85f);
            var textRenderer = label.GetComponent<MeshRenderer>();
            if (CourtyardHud.Serif)
            {
                label.font = CourtyardHud.Serif;
                textRenderer.sharedMaterial = label.font.material;
            }
            if (label.font)
            {
                label.font.RequestCharactersInTexture(label.text, label.fontSize);
                Vector3 size = textRenderer.localBounds.size;
                float fit = Mathf.Min(1f, 2.47f / Mathf.Max(0.01f, size.x), 0.34f / Mathf.Max(0.01f, size.y));
                label.characterSize *= fit;
            }
            textRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            textRenderer.receiveShadows = false;
            carvedName.SetParent(plaque, true);
            plaque.gameObject.SetActive(false);
            return plaque.gameObject;
        }

        private void ShowPlaques(bool visible)
        {
            if (namePlaques == null)
                return;
            foreach (var plaque in namePlaques)
                if (plaque)
                    plaque.SetActive(visible);
        }

        internal void Assemble(float fraction, bool announce)
        {
            if (!roadAssembly)
                return;
            roadAssembly.Apply(fraction);
            if (!announce)
                return;
            while (roadSignals < roadAssembly.tracks.Length && fraction >= roadAssembly.tracks[roadSignals].phaseEnd)
            {
                int placed = roadSignals++;
                RoadBlockPlaced?.Invoke(placed);
                if (audio)
                    audio.Play(Cue.RoadBlock);
            }
        }

        private void Update()
        {
            if (!Active)
                return;
            var key = Keyboard.current;
            if (key != null)
            {
                if (key.aKey.wasPressedThisFrame || key.leftArrowKey.wasPressedThisFrame)
                    Step(-1);
                if (key.dKey.wasPressedThisFrame || key.rightArrowKey.wasPressedThisFrame)
                    Step(1);
                if (key.eKey.wasPressedThisFrame || key.enterKey.wasPressedThisFrame)
                    Enter();
            }
            if (!audioReady && (key?.anyKey.wasPressedThisFrame == true || Pointer.current?.press.wasPressedThisFrame == true))
            {
                audioReady = true;
                if (music)
                    music.StartAudio(progress.CompletedCount + 1);
            }
            if (!Active)
                return;
            rig.ReadZoomInput();
            float dt = Time.unscaledDeltaTime;
            Move(dt);
            int nearest = 0;
            for (int i = 0; i < stations.Length; i++)
            {
                float distance = Mathf.Abs(stations[i] - along);
                if (distance < Mathf.Abs(stations[nearest] - along))
                    nearest = i;
                if (distance <= approachDistance && CanEnter(i) && districts[i].Approach(progress) && audio)
                    audio.Play(Cue.Unlock);
            }
            for (int i = 0; i < districts.Length; i++)
                districts[i].entrance.UpdateAmbience(i == nearest, audioReady);
            RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, districts[nearest].entrance.ambientLight, dt * 1.5f);
            hintAge += dt;
            if (hintDismissed || !persistNavigationHint && hintAge > 5f)
                hintAlpha = Mathf.MoveTowards(hintAlpha, 0f, dt * 1.5f);
            if (progress.IsFinished && !progress.FinaleSeen)
            {
                finaleAge += dt;
                if (finaleAge >= 1.8f)
                {
                    progress.MarkFinaleSeen();
                    progress.Save();
                }
            }
        }

        private void BeginCrossing(int direction, float from, float to)
        {
            crossingDirection = direction;
            crossingFrom = from;
            crossingTo = to;
            crossingStarted = Time.unscaledTime;
        }

        private void Move(float dt)
        {
            float remaining = stations[Destination] - along;
            if (crossingSegment >= 0 && crossingSegment + 1 < route.distance.Length &&
                (Crossing || Mathf.Abs(remaining) > 0.001f))
            {
                float start = route.distance[crossingSegment];
                float end = route.distance[crossingSegment + 1];
                // The last 1 mm still belongs to the crossing; do not strand Crossing=true at the exit.
                float sign = Crossing && Mathf.Abs(remaining) <= 0.001f ? crossingDirection : Mathf.Sign(remaining);
                float edge = sign > 0f ? start : end;
                if (!Crossing && (along - edge) * sign <= 0f && (along + Velocity * dt - edge) * sign >= 0f)
                {
                    along = edge;
                    Crossing = true;
                    BeginCrossing((int)sign, edge, sign > 0f ? end : start);
                }
                if (Crossing)
                {
                    float exit = sign > 0f ? end : start;
                    if ((int)sign != crossingDirection)
                        BeginCrossing((int)sign, along, exit);
                    // Absolute unscaled time keeps the 0.15 s beat exact even with uneven editor frames.
                    float fraction = Mathf.Clamp01((Time.unscaledTime - crossingStarted) / crossingDuration);
                    along = Mathf.Lerp(crossingFrom, crossingTo, fraction);
                    Crossing = fraction < 1f;
                    Roll(ball, route.At(along));
                    return;
                }
            }
            float desiredVelocity = Mathf.Sign(remaining) * Mathf.Min(maximumSpeed, Mathf.Sqrt(2f * acceleration * Mathf.Abs(remaining)));
            Velocity = Mathf.MoveTowards(Velocity, desiredVelocity, acceleration * dt);
            float travel = Velocity * dt;
            if (remaining == 0f && Mathf.Abs(Velocity) < 0.001f || Mathf.Sign(remaining) == Mathf.Sign(travel) &&
                Mathf.Abs(travel) >= Mathf.Abs(remaining))
            {
                along = stations[Destination];
                Velocity = 0f;
                if (Selected != Destination)
                {
                    Selected = Destination;
                    SaveArrival(Selected);
                    Arrived?.Invoke(Selected);
                }
            }
            else
                along = Mathf.Clamp(along + travel, 0f, route.Length);
            Roll(ball, route.At(along));
            float lookAhead = Mathf.Sign(remaining) * Mathf.Min(Mathf.Abs(remaining) * 0.36f, 3.2f);
            Frame(route.At(along + lookAhead), false);
        }

        private void SaveArrival(int index)
        {
            if (progress.MoveTo(index))
                progress.Save();
        }

        internal void Frame(Vector3 focus, bool snap) =>
            rig.Move(focus + cameraOffset, Quaternion.LookRotation(-cameraOffset), rig.ZoomedSize(cameraSize), snap);

        internal static void Roll(BallBody ball, Vector3 position)
        {
            var body = ball.Body;
            Vector3 travel = position - body.position;
            if (travel.sqrMagnitude > 0.000001f)
            {
                Vector3 axis = Vector3.Cross(Vector3.up, travel.normalized);
                if (axis.sqrMagnitude > 0.0001f)
                    body.rotation = Quaternion.AngleAxis(travel.magnitude * Mathf.Rad2Deg / (ball.transform.localScale.x * 0.5f), axis) * body.rotation;
            }
            body.position = position;
        }

        private void OnDistrictLayer(int layer)
        {
            if (audio)
                audio.Play(Cue.ArchitectureLayer, 0.7f + Mathf.Min(layer * 0.1f, 0.3f));
        }

        private void OnDistrictFormed(int index)
        {
            if (progress.Materialize(index))
                progress.Save();
        }

        private void OnGUI()
        {
            if (!Active || CampaignSceneParticles.IsRunning)
                return;
            var matrix = GUI.matrix;
            var color = GUI.color;
            float scale = GeometryLevel.HudScale;
            GUI.matrix = matrix * Matrix4x4.Scale(Vector3.one * scale);
            float screenWidth = Screen.width / scale;
            float center = screenWidth * 0.5f;
            float bottom = Screen.height / scale;
            try
            {
                if (GUI.Button(new Rect(20f, 20f, 110f, 34f), "Main Menu", CourtyardHud.Button))
                    CampaignScenes.OpenMenu();
                float width = Mathf.Min(444f, screenWidth - 32f);
                Rect card = new(center - width * 0.5f, bottom - 104f, width, 84f);
                DrawDistrictCard(card);
                if (hintAlpha > 0f)
                {
                    GUI.color = new Color(color.r, color.g, color.b, color.a * hintAlpha);
                    CourtyardHud.TagLabel(new Rect(center - 139f, card.y - 31f, 278f, 24f), "A / D to move   ·   Wheel to zoom");
                }
            }
            finally
            {
                GUI.matrix = matrix;
                GUI.color = color;
            }
        }

        private void DrawDistrictCard(Rect card)
        {
            int index = AtEntrance ? Selected : Destination;
            var entrance = districts[index].entrance;
            bool available = CanEnter(index);
            bool ready = available && (!requireUnlockedDestination || progress.IsMaterialized(index));
            CourtyardHud.Card(card, true);
            Rect label = new(card.x + 64f, card.y + 12f, card.width - 203f, 16f);
            string status = !AtEntrance ? "Approaching" : !available ? "Locked" :
                !ready ? "Opening" : progress.IsCompleted(index) ? "Complete" : "Ready";
            GUI.Label(label, $"Level {entrance.levelIndex + 1}   ·   {status}", CourtyardHud.Caption);
            var title = new GUIContent(entrance.districtName == $"Level {entrance.levelIndex + 1}" ? "Next district" : entrance.districtName);
            float titleWidth = label.width + (ready && AtEntrance ? 0f : 64f);
            bool compact = CourtyardHud.Title.CalcSize(title).x > titleWidth;
            GUI.Label(new Rect(label.x, label.yMax, titleWidth, compact ? 43f : 33f), title,
                compact ? CourtyardHud.CompactTitle : CourtyardHud.Title);

            if (ready && AtEntrance && GUI.Button(new Rect(card.xMax - 128f, card.y + 28f, 72f, 32f),
                "Enter  E", CourtyardHud.Primary))
                Enter();
            if (Destination > 0 && GUI.Button(new Rect(card.x + 12f, card.y + 28f, 40f, 32f), "‹  A", CourtyardHud.Button))
                Step(-1);
            if (Destination < districts.Length - 1 &&
                (!requireUnlockedDestination || CanEnter(Destination + 1)) &&
                GUI.Button(new Rect(card.xMax - 51f, card.y + 28f, 39f, 32f), "D  ›", CourtyardHud.Button))
                Step(1);
        }
    }
}
