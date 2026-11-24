#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BounceBack.GeometryWorld.Tests
{
    public sealed class CampaignHubPlayTests
    {
        private int oldProgress;
        private int oldPreviewProgress;
        private bool oldMotion;
        private bool oldFull;

        [SetUp]
        public void PreserveProgress()
        {
            oldProgress = PlayerPrefs.GetInt(CampaignProgress.SaveKey, -1);
            oldPreviewProgress = PlayerPrefs.GetInt(CampaignProgress.PreviewSaveKey, -1);
            oldMotion = CampaignSettings.ReducedMotion;
            oldFull = CampaignScenes.FullCampaign;
            CampaignScenes.ConsumeHubArrival();
            CampaignScenes.UseFullCampaign(false);
            CampaignSettings.ReducedMotion = true;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name is CampaignScenes.Boot or CampaignScenes.Hub or CampaignScenes.SystemsScene or "The Land Without the Sun")
            {
                var empty = SceneManager.CreateScene("Campaign Hub test cleanup");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            CampaignSettings.ReducedMotion = oldMotion;
            CampaignScenes.UseFullCampaign(oldFull);
            CampaignScenes.ConsumeHubArrival();
            if (oldProgress < 0)
                PlayerPrefs.DeleteKey(CampaignProgress.SaveKey);
            else
                PlayerPrefs.SetInt(CampaignProgress.SaveKey, oldProgress);
            if (oldPreviewProgress < 0)
                PlayerPrefs.DeleteKey(CampaignProgress.PreviewSaveKey);
            else
                PlayerPrefs.SetInt(CampaignProgress.PreviewSaveKey, oldPreviewProgress);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator FirstLaunchLoadsExistingPreparationAndCompletionTransfersToFourSlotCity()
        {
            CampaignSettings.ReducedMotion = false;
            new CampaignProgress(0).Save();
            Assert.That(Application.CanStreamedLevelBeLoaded(CampaignScenes.Boot), Is.True);
            yield return SceneManager.LoadSceneAsync(CampaignScenes.Boot);
            var menu = Object.FindFirstObjectByType<GeometricMenu>();
            float wideSize = menu.view.orthographicSize;
            yield return Await(() => menu && menu.view.orthographicSize < wideSize * 0.60f, "menu camera approaches the launch mechanism");
            yield return Await(() => SceneManager.GetActiveScene().name == CampaignScenes.SystemsScene, "first launch tutorial");
            var tutorial = Object.FindFirstObjectByType<CampaignPlayground>();
            yield return Await(() => tutorial && tutorial.InTransition && tutorial.level.Ball, "close framing on the tower ball");
            Assert.That(tutorial.rig.view.orthographicSize, Is.LessThan(tutorial.rig.overviewSize * 0.50f));
            Assert.That(tutorial.level.InputEnabled, Is.False);
            tutorial.level.ReleaseBall();
            Assert.That(tutorial.level.Ball.Released, Is.False);
            yield return Await(() => tutorial && tutorial.InPuzzle, "authored tutorial preparation");
            Assert.That(tutorial.rig.view.orthographicSize, Is.EqualTo(tutorial.rig.overviewSize).Within(0.025f));
            Assert.That(tutorial.level.InputEnabled, Is.True);
            Assert.That(tutorial.level.Ball.Released, Is.False);
            tutorial.level.Ball.Hold(tutorial.completionPlate.surface.position + Vector3.up * 0.4f, Quaternion.identity);
            tutorial.level.ReachGoal();
            yield return Await(() => SceneManager.GetActiveScene().name == CampaignScenes.Hub, "handoff to campaign city");
            var hub = Object.FindFirstObjectByType<CampaignHub>();
            yield return Await(() => hub && hub.Ready && hub.city.Active, "road construction");
            Assert.That(hub.city.districts.Length, Is.EqualTo(4));
            Assert.That(hub.city.Destination, Is.EqualTo(1));
            Assert.That(CampaignProgress.Load().IsUnlocked(1), Is.True);
            Assert.That(CampaignProgress.Load().TutorialSeen, Is.False);

            yield return Await(() => hub.city.AtEntrance && hub.city.Selected == 1 &&
                CampaignProgress.Load().IsMaterialized(1) && !CampaignScenes.IsLoading, "arrival and reveal at the second district");
            Assert.That(hub.city.districts[1].entrance.districtName, Is.EqualTo("The Land Without the Sun"));
            Assert.That(hub.city.Enter(), Is.True);
            yield return Await(() => CampaignSceneParticles.IsRunning, "cross-scene particle transition");
            var effect = Array.Find(Resources.FindObjectsOfTypeAll<CampaignSceneParticles>(), item => item.isActiveAndEnabled);
            Assert.That(effect, Is.Not.Null);
            var field = effect.GetComponent<ParticleSystem>();
            yield return Await(() => field && field.particleCount > 0, "stone particles emitted");
            Assert.That(field.GetComponent<ParticleSystemRenderer>().renderMode, Is.EqualTo(ParticleSystemRenderMode.Mesh));
            yield return Await(() => SceneManager.GetActiveScene().name == "The Land Without the Sun", "authored second level loads");
            var puzzle = Object.FindFirstObjectByType<SunlessCourtyard>();
            Assert.That(puzzle, Is.Not.Null);
            var adapter = puzzle.GetComponent<CampaignLevelAdapter>();
            yield return Await(() => adapter && adapter.Phase == CampaignPhase.Preparation &&
                !CampaignScenes.IsLoading, "second level assembly and preparation");
            Assert.That(puzzle.tablets[0].GetComponentInChildren<MeshRenderer>().forceRenderingOff, Is.False);
            Assert.That(adapter.Slot, Is.EqualTo(1));
            adapter.TogglePause();
            Assert.That(adapter.Phase, Is.EqualTo(CampaignPhase.Paused));
            adapter.TogglePause();
            Assert.That(adapter.Phase, Is.EqualTo(CampaignPhase.Preparation));

            adapter.ReportReleased();
            adapter.puzzle.ReachGoal();
            yield return Await(() => CampaignSceneParticles.IsRunning, "second level return transition");
            yield return Await(() => SceneManager.GetActiveScene().name == CampaignScenes.Hub, "second level returns to the city");
            yield return Await(() => !CampaignScenes.IsLoading, "city assembly completes");
            Assert.That(CampaignProgress.Load().IsCompleted(1), Is.True);
        }

        [UnityTest]
        public IEnumerator ReversesMaterializesEntersAndRestoresSavedStation()
        {
            new CampaignProgress(0).Save();
            var save = new CampaignProgress(0, preview: true);
            save.Complete(0);
            save.Materialize(0);
            save.Save();
            yield return SceneManager.LoadSceneAsync(CampaignScenes.Hub);
            var hub = Object.FindFirstObjectByType<CampaignHub>();
            yield return Await(() => hub && hub.Ready, "hub startup");
            Assert.That(hub.SystemsPreview, Is.True);
            var city = hub.city;
            Assert.That(city.districts[2].entrance.wire.activeSelf, Is.True);
            Assert.That(city.Step(1), Is.True);
            Assert.That(city.Step(1), Is.False, "The next locked district is not a navigation destination.");
            yield return Await(() => city.Velocity > 1f, "forward acceleration");
            Assert.That(city.Step(-1), Is.True);
            Assert.That(city.Velocity, Is.Positive, "Direction must change through deceleration.");
            Assert.That(city.Enter(), Is.False);
            yield return Await(() => city.Velocity < -0.02f, "reverse acceleration");
            yield return Await(() => city.AtEntrance && city.Selected == 0, "first dock");

            hub.destinations[1].scenePath = "Assets/Scenes/Unpublished District.unity";
            Assert.That(city.Step(1), Is.True);
            yield return Await(() => city.AtEntrance && city.Selected == 1 && PreviewProgress().IsMaterialized(1), "second dock and reveal");
            Assert.That(city.districts[1].entrance.wire.activeSelf, Is.False);
            Assert.That(PreviewProgress().TutorialSeen, Is.True);
            Assert.That(city.Enter(), Is.True);
            yield return null;
            hub.enabled = false;
            hub.enabled = true;
            Assert.That(hub.Phase == CampaignPhase.LevelSelect && city.AtEntrance, Is.True);
            Assert.That(hub.carrier.gameObject.activeSelf, Is.False);

            hub.trialArchitecture = null;
            hub.completionRipple = null;
            Assert.That(city.Enter(), Is.True);
            Assert.That(city.Enter(), Is.False);
            yield return Await(() => hub.Phase == CampaignPhase.Preparation, "mock preparation");
            Assert.That(hub.HasSceneFallback, Is.True);
            Assert.That(hub.ball.Body.isKinematic && !hub.ball.Released, Is.True);
            for (int i = 0; i < 4; i++)
                yield return null;
            Assert.That(hub.Phase, Is.EqualTo(CampaignPhase.Preparation));

            Assert.That(hub.Release(), Is.True);
            Assert.That(hub.ReportFailure(), Is.True);
            Assert.That(hub.PrepareAgain(), Is.True);
            Assert.That(hub.ReturnToCity(), Is.True);
            yield return Await(() => hub.Phase == CampaignPhase.LevelSelect, "fold back");
            Assert.That(city.Selected, Is.EqualTo(1));
            Assert.That(city.Enter(), Is.True);
            yield return Await(() => hub.Phase == CampaignPhase.Preparation, "second entry");
            Assert.That(hub.Release(), Is.True);
            yield return Await(() => hub.Phase == CampaignPhase.LevelSelect, "mock completion");
            Assert.That(PreviewProgress().IsUnlocked(2), Is.True);
            Assert.That(PreviewProgress().Position, Is.EqualTo(1));
            Assert.That(CampaignProgress.Load().CompletedCount, Is.Zero, "Preview progress stays separate from the campaign.");

            yield return SceneManager.LoadSceneAsync(CampaignScenes.Hub);
            var reopened = Object.FindFirstObjectByType<CampaignHub>();
            yield return Await(() => reopened && reopened.Ready && reopened.city.Active, "reopen saved city");
            Assert.That(reopened.city.Selected, Is.EqualTo(1));
            Assert.That(reopened.city.districts[1].entrance.solid.gameObject.activeSelf, Is.True);
            Assert.That(reopened.city.districts[1].IsRevealing, Is.False);
        }

        [UnityTest]
        public IEnumerator ReturningBootOpensSettingsAndContinuesAtSavedEntrance()
        {
            var save = new CampaignProgress(0);
            save.Complete(0);
            save.MoveTo(1);
            save.Save();
            yield return SceneManager.LoadSceneAsync(CampaignScenes.Boot);
            var menu = Object.FindFirstObjectByType<GeometricMenu>();
            Assert.That(menu, Is.Not.Null);
            yield return Await(() => menu.NavigationAvailable, "menu presentation");
            for (int i = 0; i < 3; i++)
            {
                Assert.That(menu.FocusNextDevice(), Is.True);
                Assert.That(menu.FocusedDevice, Is.SameAs(menu.devices[i]));
            }
            Assert.That(menu.ActivateFocused(), Is.True);
            yield return Await(() => menu.SettingsVisible, "settings opens");
            Assert.That(menu.CloseOverlay(), Is.True);
            yield return Await(() => menu.NavigationAvailable, "settings closes");
            Assert.That(menu.FocusNextDevice(), Is.True);
            Assert.That(menu.FocusNextDevice(), Is.True);
            Assert.That(menu.FocusedDevice.action, Is.EqualTo(MenuAction.Continue));
            Assert.That(menu.ActivateFocused(), Is.True);
            yield return Await(() => SceneManager.GetActiveScene().name == CampaignScenes.Hub, "returning player Continue");
            var hub = Object.FindFirstObjectByType<CampaignHub>();
            yield return Await(() => hub && hub.Ready && hub.city.Active, "resume saved entrance");
            Assert.That(hub.city.Selected, Is.EqualTo(1));
        }

        [Test]
        public void SplitVolumesAndExpandedProgressUsePersistentSettings()
        {
            float music = CampaignSettings.MusicVolume;
            float effects = CampaignSettings.EffectsVolume;
            const string expanded = "BounceBack.Campaign.v2";
            string backup = PlayerPrefs.GetString(expanded);
            bool hadExpanded = PlayerPrefs.HasKey(expanded);
            try
            {
                CampaignSettings.MusicVolume = 0.23f;
                CampaignSettings.EffectsVolume = 0.71f;
                CampaignSettings.SavePending(true);
                Assert.That(CampaignSettings.MusicVolume, Is.EqualTo(0.23f));
                Assert.That(CampaignSettings.EffectsVolume, Is.EqualTo(0.71f));
                PlayerPrefs.SetString(expanded, "{bad-data");
                new CampaignProgress(0b1111).Save();
                var save = CampaignProgress.Load(6);
                Assert.That(save.CompletedCount, Is.EqualTo(4));
                Assert.That(save.Complete(4), Is.True);
                Assert.That(save.Materialize(5), Is.True);
                Assert.That(save.MoveTo(5), Is.True);
                save.Save();
                var restored = CampaignProgress.Load(6);
                Assert.That(restored.CompletedCount, Is.EqualTo(5));
                Assert.That(restored.Position, Is.EqualTo(5));
                Assert.That(restored.IsMaterialized(5), Is.True);
            }
            finally
            {
                CampaignSettings.MusicVolume = music;
                CampaignSettings.EffectsVolume = effects;
                if (hadExpanded)
                    PlayerPrefs.SetString(expanded, backup);
                else
                    PlayerPrefs.DeleteKey(expanded);
            }
        }

        private static IEnumerator Await(Func<bool> condition, string operation)
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(condition(), Is.True, $"Timed out: {operation}");
        }

        private static CampaignProgress PreviewProgress() => CampaignProgress.Load(CampaignProgress.LevelCount, true);
    }
}
#endif
