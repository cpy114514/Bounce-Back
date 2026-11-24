using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BounceBack.GeometryWorld
{
    public sealed class CampaignProgress
    {
        public const int LevelCount = 4;
        internal const string SaveKey = "BounceBack.Campaign.v1";
        internal const string PreviewSaveKey = "BounceBack.Campaign.Preview.v1";
        private const string ExpandedSaveKey = "BounceBack.Campaign.v2";
        private const string ExpandedPreviewSaveKey = "BounceBack.Campaign.Preview.v2";
        private const int FinaleFlag = 1 << LevelCount;
        private const int MaterializedShift = LevelCount + 1;
        private const int PositionShift = MaterializedShift + LevelCount;
        private readonly HashSet<int> materialized = new();
        private readonly int levelCount;
        private readonly bool preview;

        public int CompletedCount { get; private set; }
        public bool IsFinished => CompletedCount >= levelCount;
        public bool FinaleSeen { get; private set; }
        public bool TutorialSeen { get; private set; }
        public int Position { get; private set; }

        [Serializable]
        private sealed class SaveData
        {
            public int version = 2;
            public int completed;
            public int position;
            public bool finale;
            public bool tutorial;
            public int[] formed;
        }

        internal CampaignProgress(int data, int count = LevelCount, bool preview = false)
        {
            levelCount = Mathf.Max(1, count);
            this.preview = preview;
            if (data < 0)
                return;
            while (CompletedCount < LevelCount && (data & (1 << CompletedCount)) != 0)
                CompletedCount++;
            CompletedCount = Mathf.Min(CompletedCount, levelCount);
            FinaleSeen = IsFinished && (data & FinaleFlag) != 0;
            TutorialSeen = (data & (1 << (PositionShift + 2))) != 0;
            for (int i = 0; i < Mathf.Min(levelCount, LevelCount); i++)
                if ((data & (1 << (MaterializedShift + i))) != 0 && IsUnlocked(i))
                    materialized.Add(i);
            Position = (data >> PositionShift) & 3;
        }

        public static CampaignProgress Load() => new(PlayerPrefs.GetInt(SaveKey, 0));

        public static bool HasStoredJourney => PlayerPrefs.HasKey(SaveKey) || PlayerPrefs.HasKey(ExpandedSaveKey);

        public static bool CanResume
        {
            get
            {
                int flags = PlayerPrefs.GetInt(SaveKey, -1);
                int completed = flags & ((1 << LevelCount) - 1);
                return flags >= 0 && flags < 1 << (PositionShift + 3) &&
                    (completed & (completed + 1)) == 0;
            }
        }

        internal static void StartJourney()
        {
            PlayerPrefs.DeleteKey(ExpandedSaveKey);
            new CampaignProgress(0).Save();
        }

        public static CampaignProgress Load(int count, bool preview = false)
        {
            var progress = new CampaignProgress(PlayerPrefs.GetInt(preview ? PreviewSaveKey : SaveKey), count, preview);
            if (count <= LevelCount)
                return progress;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(preview ? ExpandedPreviewSaveKey : ExpandedSaveKey));
                if (data == null || data.version != 2)
                    return progress;
                progress.CompletedCount = Mathf.Clamp(data.completed, progress.CompletedCount, count);
                progress.Position = Mathf.Clamp(data.position, 0, count - 1);
                progress.FinaleSeen = data.finale && progress.IsFinished;
                progress.TutorialSeen |= data.tutorial;
                foreach (int index in data.formed ?? Array.Empty<int>())
                    progress.Materialize(index);
            }
            catch (ArgumentException)
            {
                // The legacy prefix remains available if the expanded save is damaged.
            }
            return progress;
        }

        public bool IsUnlocked(int index) => index >= 0 && index < levelCount && index <= CompletedCount;
        public bool IsCompleted(int index) => index >= 0 && index < CompletedCount;
        public bool IsMaterialized(int index) => IsUnlocked(index) && materialized.Contains(index);

        public bool Materialize(int index)
        {
            return IsUnlocked(index) && materialized.Add(index);
        }

        public bool MoveTo(int index)
        {
            if (index < 0 || index >= levelCount || index == Position)
                return false;
            Position = index;
            return true;
        }

        public bool Complete(int index)
        {
            if (!IsUnlocked(index) || IsCompleted(index))
                return false;
            CompletedCount++;
            return true;
        }

        public void MarkFinaleSeen()
        {
            if (IsFinished)
                FinaleSeen = true;
        }

        public void MarkTutorialSeen() => TutorialSeen = true;

        internal int Encode()
        {
            int flags = 0;
            for (int i = 0; i < LevelCount; i++)
                if (materialized.Contains(i))
                    flags |= 1 << (MaterializedShift + i);
            return ((1 << Mathf.Min(CompletedCount, LevelCount)) - 1) | (FinaleSeen ? FinaleFlag : 0) | flags |
                (Mathf.Min(Position, LevelCount - 1) << PositionShift) | (TutorialSeen ? 1 << (PositionShift + 2) : 0);
        }

        public void Save()
        {
            PlayerPrefs.SetInt(preview ? PreviewSaveKey : SaveKey, Encode());
            if (levelCount > LevelCount)
            {
                var formed = new int[materialized.Count];
                materialized.CopyTo(formed);
                PlayerPrefs.SetString(preview ? ExpandedPreviewSaveKey : ExpandedSaveKey, JsonUtility.ToJson(new SaveData
                {
                    completed = CompletedCount, position = Position, finale = FinaleSeen, tutorial = TutorialSeen, formed = formed
                }));
            }
            PlayerPrefs.Save();
        }
    }

    public static class CampaignScenes
    {
        public const string Menu = "Geometric Menu";
        public const string Tutorial = "Santorini";
        internal const string SystemsScene = "Santorini Gameplay";
        public const string World = "Floating City";
        public const string Prototype = "Prototype Trial";
        public const string Boot = "Campaign Boot";
        public const string Hub = "Campaign City";
        public const string MainMenuPath = "Assets/Scenes/Main Menu.unity";
        public const string ArrivalPreviewPath = "Assets/Scenes/Arrival Preview.unity";

        public static bool FullCampaign { get; private set; }
        internal static bool UsingSystemsPreview { get; private set; }
        public static int CurrentLevel { get; private set; }
        public static int WorldEntrance { get; private set; } = -1;
        public static bool PlayUnlockReveal { get; private set; }
        internal static bool StartsInCity { get; private set; }
        internal static bool HasSystemsScene => Application.CanStreamedLevelBeLoaded(SystemsScene);
        private static AsyncOperation loading;
        private static Quaternion? menuArrival;
        private static bool mainMenuActive;
        internal const float ArrivalFraming = 3.7f;
        public static bool IsLoading => CampaignSceneParticles.IsRunning || loading != null && !loading.isDone;

        public static bool Enter(int index) => Enter(index, null);

        internal static bool EnterFromMenu(Quaternion heading) => Enter(0, heading);

        internal static Quaternion? ConsumeMenuArrival()
        {
            var heading = menuArrival;
            menuArrival = null;
            return heading;
        }

        private static bool Enter(int index, Quaternion? heading)
        {
            if (!CampaignProgress.Load().IsUnlocked(index) || HasSystemsScene && index != 0)
                return false;
            string scene = HasSystemsScene ? SystemsScene : index == 0 ? Tutorial : Prototype;
            return TryLoad(scene, () =>
            {
                menuArrival = scene == SystemsScene ? heading : null;
                StartsInCity = false;
                CurrentLevel = index;
            });
        }

        public static bool OpenWorld(int entrance = -1, bool unlockReveal = false)
        {
            if (FullCampaign && Application.CanStreamedLevelBeLoaded(Hub))
                return TryLoad(Hub, () =>
                {
                    WorldEntrance = entrance;
                    PlayUnlockReveal = unlockReveal;
                    StartsInCity = false;
                }, useParticles: true);
            bool useSystems = HasSystemsScene;
            return TryLoad(useSystems ? SystemsScene : World, () =>
            {
                WorldEntrance = !useSystems && entrance < 0 ? Mathf.Max(0, CampaignProgress.Load().CompletedCount - 1) : entrance;
                PlayUnlockReveal = unlockReveal;
                StartsInCity = useSystems;
            });
        }
        public static bool OpenMenu() => TryLoad(mainMenuActive && Application.CanStreamedLevelBeLoaded(MainMenuPath)
            ? MainMenuPath : FullCampaign && Application.CanStreamedLevelBeLoaded(Boot) ? Boot : Menu,
            useParticles: FullCampaign);

        internal static void UseMainMenu()
        {
            mainMenuActive = true;
            UseFullCampaign();
        }

        public static bool CanBeginMenuJourney => HasSystemsScene || Application.CanStreamedLevelBeLoaded(ArrivalPreviewPath);

        public static bool BeginMenuJourney(Quaternion heading)
        {
            string destination = HasSystemsScene ? SystemsScene : ArrivalPreviewPath;
            return TryLoad(destination, () =>
            {
                CampaignProgress.StartJourney();
                SelectLevel(0);
                menuArrival = HasSystemsScene ? heading : null;
            });
        }

        public static bool ResumeMenuJourney(Quaternion heading)
        {
            if (!CampaignProgress.CanResume)
                return false;
            if (FullCampaign && CampaignProgress.Load().IsCompleted(0) && Application.CanStreamedLevelBeLoaded(Hub))
                return OpenWorld(CampaignProgress.Load().Position);
            if (HasSystemsScene)
                return EnterFromMenu(heading);
            return TryLoad(ArrivalPreviewPath);
        }
        internal static void UseFullCampaign(bool enabled = true, bool preview = false)
        {
            FullCampaign = enabled;
            UsingSystemsPreview = preview;
        }
        internal static (int index, bool reveal) ConsumeHubArrival()
        {
            var arrival = (WorldEntrance, PlayUnlockReveal);
            WorldEntrance = -1;
            PlayUnlockReveal = false;
            return arrival;
        }
        internal static bool EnterExternal(string scene, int index) => TryLoad(scene, () => SelectLevel(index), useParticles: true);
        internal static void SelectLevel(int index)
        {
            StartsInCity = false;
            CurrentLevel = index;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            CurrentLevel = 0;
            WorldEntrance = -1;
            PlayUnlockReveal = false;
            StartsInCity = false;
            FullCampaign = false;
            UsingSystemsPreview = false;
            mainMenuActive = false;
            menuArrival = null;
            loading = null;
        }

        private static bool TryLoad(string scene, Action prepare = null, bool useParticles = false)
        {
            if (IsLoading)
                return false;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogWarning($"Add the {scene} scene to the active Build Profile.");
                return false;
            }
            var current = SceneManager.GetActiveScene();
            if (useParticles && !CampaignSettings.ReducedMotion && scene != current.name && scene != current.path &&
                CampaignSceneParticles.TryPlay(Load))
                return true;
            return Load() != null;

            AsyncOperation Load()
            {
                prepare?.Invoke();
                loading = SceneManager.LoadSceneAsync(scene);
                return loading;
            }
        }
    }
}
